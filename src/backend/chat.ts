import { apiRequest } from './http';
import { liveQuery, type Unsubscribe } from './realtime';
import { CHAT_PAGE_SIZE } from '../shared/constants';
import { decryptText, encryptText } from '../shared/crypto';
import type { ChatMessage } from '../shared/types';

/** A message as the server holds it: still encrypted. */
interface EncryptedMessage {
  id: string;
  roomId: string;
  senderUid: string;
  senderName: string;
  iv: string;
  ct: string;
  createdAt: number;
}

/**
 * Encrypt and post a chat message. The server stores only ciphertext; it fills in
 * the sender from the token, so a client cannot post as someone else.
 */
export async function sendMessage(roomId: string, key: CryptoKey, text: string): Promise<void> {
  const body = text.trim();
  if (!body) return;

  const { iv, ct } = await encryptText(key, body);
  await apiRequest<{ id: string }>('POST', `/rooms/${roomId}/messages`, { body: { iv, ct } });
}

async function decrypt(key: CryptoKey, message: EncryptedMessage): Promise<ChatMessage> {
  let text: string;
  try {
    text = await decryptText(key, { iv: message.iv, ct: message.ct });
  } catch {
    // Key rotated or payload corrupt: show a placeholder rather than silently drop it.
    text = '🔒 (could not decrypt)';
  }

  return {
    id: message.id,
    senderUid: message.senderUid,
    senderName: message.senderName || 'Someone',
    text,
    at: message.createdAt,
  };
}

/** Appends a pushed message, keeping the transcript in order and bounded. */
function appendMessage(messages: EncryptedMessage[], pushed: EncryptedMessage): EncryptedMessage[] {
  if (messages.some((message) => message.id === pushed.id)) return messages;

  const appended = [...messages, pushed].sort((left, right) => left.createdAt - right.createdAt);
  return appended.slice(-CHAT_PAGE_SIZE);
}

/**
 * Subscribe to a room's chat, decrypting as messages arrive, oldest first.
 *
 * Decryption is async, so two updates close together could settle out of order and
 * an older list overwrite a newer one. Results are sequenced; late ones are dropped.
 */
export function watchMessages(
  roomId: string,
  key: CryptoKey,
  callback: (messages: ChatMessage[]) => void,
  onError?: (error: Error) => void,
): Unsubscribe {
  let issued = 0;
  let delivered = 0;

  const deliver = async (encrypted: EncryptedMessage[]) => {
    const sequence = ++issued;
    const decrypted = await Promise.all(encrypted.map((message) => decrypt(key, message)));
    if (sequence < delivered) return;
    delivered = sequence;
    callback(decrypted);
  };

  return liveQuery<EncryptedMessage[], EncryptedMessage>({
    topic: `chat:${roomId}`,
    event: 'message',
    load: () => apiRequest<EncryptedMessage[]>('GET', `/rooms/${roomId}/messages?limit=${CHAT_PAGE_SIZE}`),
    applyPush: (current, pushed) => (pushed.roomId === roomId ? appendMessage(current, pushed) : undefined),
    emit: (encrypted) => void deliver(encrypted),
    onError,
  });
}
