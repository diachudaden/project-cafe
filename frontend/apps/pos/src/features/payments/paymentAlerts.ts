// Âm báo "tink tink" + đọc "Đã nhận X đồng". Không cần file mp3: tự tổng hợp bằng Web Audio.
//
// Trình duyệt chặn phát âm thanh cho tới khi người dùng tương tác với trang lần đầu.
// Gọi unlockAudio() trong handler click đầu tiên (ví dụ nút "Mở ca"), hoặc dùng installAudioUnlock().

let ctx: AudioContext | null = null;

function audio(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  if (!ctx) {
    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return null;
    ctx = new Ctor();
  }
  return ctx;
}

export function unlockAudio(): void {
  const a = audio();
  if (a && a.state === 'suspended') void a.resume();
  // Một số trình duyệt cũng cần "mồi" speechSynthesis trong user gesture.
  if ('speechSynthesis' in window) window.speechSynthesis.getVoices();
}

/** Tự mở khoá âm thanh ở lần click/chạm đầu tiên. Trả về hàm gỡ listener. */
export function installAudioUnlock(): () => void {
  const handler = () => {
    unlockAudio();
    remove();
  };
  const remove = () => {
    window.removeEventListener('pointerdown', handler);
    window.removeEventListener('keydown', handler);
  };
  window.addEventListener('pointerdown', handler);
  window.addEventListener('keydown', handler);
  return remove;
}

function blip(a: AudioContext, at: number, freq: number) {
  const osc = a.createOscillator();
  const gain = a.createGain();
  osc.type = 'sine';
  osc.frequency.setValueAtTime(freq, at);
  gain.gain.setValueAtTime(0.0001, at);
  gain.gain.exponentialRampToValueAtTime(0.35, at + 0.01);
  gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.25);
  osc.connect(gain).connect(a.destination);
  osc.start(at);
  osc.stop(at + 0.3);
}

/** Hai tiếng "tink" ngắn, nốt sau cao hơn. */
export function playTinkTink(): void {
  const a = audio();
  if (!a) return;
  if (a.state === 'suspended') void a.resume();
  const t = a.currentTime + 0.02;
  blip(a, t, 1568); // G6
  blip(a, t + 0.16, 2093); // C7
}

/** Đọc thành tiếng bằng giọng tiếng Việt nếu máy có; không có thì bỏ qua (âm báo vẫn kêu). */
export function speakVietnamese(text: string): void {
  if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
  const synth = window.speechSynthesis;
  const voice = synth.getVoices().find((v) => v.lang.toLowerCase().startsWith('vi'));
  if (!voice) return;

  const u = new SpeechSynthesisUtterance(text);
  u.voice = voice;
  u.lang = voice.lang;
  u.rate = 1;
  synth.cancel();
  synth.speak(u);
}

export function announcePayment(amount: number): void {
  playTinkTink();
  // Đợi tiếng tink xong rồi mới đọc.
  window.setTimeout(() => speakVietnamese(`Đã nhận ${amount.toLocaleString('vi-VN')} đồng`), 450);
}
