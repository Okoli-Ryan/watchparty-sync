// A stand-in for @microsoft/signalr's HubConnection, so the realtime layer can be
// tested without a server: tests push messages and simulate reconnects by hand.

type Handler = (payload: unknown) => void;

export class FakeHubConnection {
  state = 'Disconnected';
  readonly invocations: Array<{ method: string; argument: unknown }> = [];
  private readonly handlers = new Map<string, Handler[]>();
  private reconnectedCallback: (() => void) | null = null;

  async start(): Promise<void> {
    this.state = 'Connected';
  }

  async stop(): Promise<void> {
    this.state = 'Disconnected';
  }

  on(event: string, handler: Handler): void {
    const existing = this.handlers.get(event) ?? [];
    this.handlers.set(event, [...existing, handler]);
  }

  onreconnected(callback: () => void): void {
    this.reconnectedCallback = callback;
  }

  async invoke(method: string, argument: unknown): Promise<boolean> {
    this.invocations.push({ method, argument });
    return true;
  }

  /** What the server would do: send `payload` to every `event` handler. */
  push(event: string, payload: unknown): void {
    for (const handler of this.handlers.get(event) ?? []) handler(payload);
  }

  /** What SignalR does after a dropped connection comes back. */
  simulateReconnect(): void {
    this.reconnectedCallback?.();
  }

  subscribedTopics(): unknown[] {
    return this.invocations
      .filter((invocation) => invocation.method === 'Subscribe')
      .map((invocation) => invocation.argument);
  }
}

export const fakeHubs: FakeHubConnection[] = [];

/** The module factory for `vi.mock('@microsoft/signalr', ...)`. */
export function fakeSignalRModule() {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      const hub = new FakeHubConnection();
      fakeHubs.push(hub);
      return hub;
    }
  }

  return {
    HubConnectionBuilder,
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
    LogLevel: { Warning: 3 },
  };
}

/** Lets queued promise callbacks (fetches, invokes) run before asserting. */
export async function settle(): Promise<void> {
  for (let round = 0; round < 10; round++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
}
