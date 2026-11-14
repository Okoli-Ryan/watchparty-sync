import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
} from '@microsoft/signalr';
import { REALTIME_HUB_URL } from './config';
import { onSessionChange, readSession } from './session';
import { warn } from '../shared/log';

// ---------------------------------------------------------------------------
// The push channel that replaces Firestore's onSnapshot.
//
// The API sends four kinds of message, each carrying fresh state:
//   room     a RoomDto            (to `room:{id}` and to `rooms`)
//   members  a MembersSnapshotDto (to `members:{id}`)
//   message  a ChatMessageDto     (to `chat:{id}`)
//   history  a HistoryChangeDto   (to `history`, the caller's own)
//
// A live query subscribes to its topic FIRST, then loads the current state over
// REST, then applies pushes on top. After any reconnect it reloads from scratch:
// a push lost while disconnected is never relied on (DECISIONS.md #21).
// ---------------------------------------------------------------------------

export type Unsubscribe = () => void;

interface LiveQueryOptions<TState, TPush> {
  topic: string;
  event: 'room' | 'members' | 'message' | 'history';
  /** Fetches the complete current state. */
  load: () => Promise<TState>;
  /** Folds one push into the state; return undefined to ignore it. */
  applyPush: (current: TState, push: TPush) => TState | undefined;
  /** Receives every new state. */
  emit: (state: TState) => void;
  onError?: (error: Error) => void;
}

interface Subscription {
  topic: string;
  event: string;
  receivePush: (push: unknown) => void;
  reload: () => void;
}

/** Reconnect forever, backing off to 30s: a watch party can outlast any outage. */
const keepRetrying: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (context) => {
    const delays = [0, 2_000, 5_000, 10_000];
    return delays[context.previousRetryCount] ?? 30_000;
  },
};

const subscriptions = new Set<Subscription>();
let connection: HubConnection | null = null;
let starting: Promise<void> | null = null;

function buildConnection(): HubConnection {
  const hub = new HubConnectionBuilder()
    .withUrl(REALTIME_HUB_URL, {
      accessTokenFactory: async () => (await readSession())?.accessToken ?? '',
      // Bearer tokens, not cookies: nothing for the browser to attach.
      withCredentials: false,
    })
    .withAutomaticReconnect(keepRetrying)
    .configureLogging(LogLevel.Warning)
    .build();

  for (const event of ['room', 'members', 'message', 'history']) {
    hub.on(event, (push: unknown) => {
      for (const subscription of subscriptions) {
        if (subscription.event === event) subscription.receivePush(push);
      }
    });
  }

  hub.onreconnected(() => void resubscribeAll());
  return hub;
}

async function resubscribeAll() {
  const hub = connection;
  if (!hub) return;

  const topics = new Set([...subscriptions].map((subscription) => subscription.topic));
  for (const topic of topics) {
    await hub.invoke('Subscribe', topic).catch((error) => warn('bg', `resubscribe ${topic} failed`, error));
  }

  for (const subscription of subscriptions) subscription.reload();
}

/** Starts the connection if needed. Retries a failed first start until it succeeds. */
async function ensureConnected(): Promise<HubConnection> {
  connection ??= buildConnection();
  const hub = connection;

  if (hub.state === HubConnectionState.Connected) return hub;

  starting ??= (async () => {
    let attempt = 0;
    while (hub.state === HubConnectionState.Disconnected) {
      try {
        await hub.start();
      } catch (error) {
        const delay = keepRetrying.nextRetryDelayInMilliseconds({
          previousRetryCount: attempt,
          elapsedMilliseconds: 0,
          retryReason: error as Error,
        });
        attempt += 1;
        await new Promise((resolve) => setTimeout(resolve, delay ?? 30_000));
      }
    }
  })().finally(() => {
    starting = null;
  });

  await starting;
  return hub;
}

/** A different user, or nobody: drop the connection so the next one carries the right token. */
onSessionChange(() => {
  const previous = connection;
  connection = null;
  void previous?.stop();
  if (subscriptions.size > 0) void ensureConnected().then(resubscribeAll);
});

/**
 * Subscribe to a piece of live state. Returns a function that stops it, like
 * Firestore's onSnapshot did.
 */
export function liveQuery<TState, TPush>(options: LiveQueryOptions<TState, TPush>): Unsubscribe {
  let state: TState | undefined;
  let loaded = false;
  let stopped = false;
  let loadSequence = 0;
  const earlyPushes: TPush[] = [];

  const reload = () => {
    const sequence = ++loadSequence;
    options
      .load()
      .then((fresh) => {
        // A newer reload already started: this answer is out of date.
        if (stopped || sequence !== loadSequence) return;

        state = fresh;
        loaded = true;
        for (const push of earlyPushes.splice(0)) {
          state = options.applyPush(state, push) ?? state;
        }
        options.emit(state);
      })
      .catch((error: Error) => options.onError?.(error));
  };

  const subscription: Subscription = {
    topic: options.topic,
    event: options.event,
    receivePush: (push) => {
      if (stopped) return;
      if (!loaded || state === undefined) {
        earlyPushes.push(push as TPush);
        return;
      }

      const next = options.applyPush(state, push as TPush);
      if (next === undefined) return;
      state = next;
      options.emit(state);
    },
    reload,
  };

  subscriptions.add(subscription);

  ensureConnected()
    .then((hub) => hub.invoke('Subscribe', options.topic))
    .catch((error: Error) => options.onError?.(error))
    .finally(() => {
      if (!stopped) reload();
    });

  return () => {
    stopped = true;
    subscriptions.delete(subscription);

    const topicStillWanted = [...subscriptions].some((other) => other.topic === options.topic);
    if (!topicStillWanted && connection?.state === HubConnectionState.Connected) {
      void connection.invoke('Unsubscribe', options.topic).catch(() => undefined);
    }
  };
}
