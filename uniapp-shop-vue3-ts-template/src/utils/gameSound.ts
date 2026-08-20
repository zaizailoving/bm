type SfxName = 'start' | 'pause' | 'resume' | 'success' | 'pop' | 'charge' | 'fruit' | 'balloon' | 'error'

type AudioContextLike = AudioContext & {
  webkitAudioContext?: typeof AudioContext
}

let ctx: AudioContext | null = null
let muted = false
let bgmTimer: ReturnType<typeof setInterval> | null = null
let bgmStep = 0
let bgmKind = 'cute'

const bgmScales: Record<string, number[]> = {
  cute: [523.25, 659.25, 783.99, 659.25, 587.33, 698.46, 880, 698.46],
  sky: [392, 493.88, 587.33, 783.99, 659.25, 587.33, 493.88, 392],
  green: [440, 523.25, 659.25, 783.99, 659.25, 587.33, 523.25, 440],
}

function getCtx() {
  if (typeof window === 'undefined') return null
  if (!ctx) {
    const AudioCtor = window.AudioContext || (window as unknown as AudioContextLike).webkitAudioContext
    if (!AudioCtor) return null
    ctx = new AudioCtor()
  }
  if (ctx.state === 'suspended') {
    void ctx.resume()
  }
  return ctx
}

function tone(freq: number, duration = 0.12, gainValue = 0.04, type: OscillatorType = 'sine', delay = 0) {
  if (muted) return
  const audio = getCtx()
  if (!audio) return

  const now = audio.currentTime + delay
  const osc = audio.createOscillator()
  const gain = audio.createGain()

  osc.type = type
  osc.frequency.setValueAtTime(freq, now)
  gain.gain.setValueAtTime(0.0001, now)
  gain.gain.exponentialRampToValueAtTime(gainValue, now + 0.018)
  gain.gain.exponentialRampToValueAtTime(0.0001, now + duration)

  osc.connect(gain)
  gain.connect(audio.destination)
  osc.start(now)
  osc.stop(now + duration + 0.02)
}

export function setGameSoundMuted(next: boolean) {
  muted = next
  if (muted) stopGameBgm()
}

export function isGameSoundMuted() {
  return muted
}

export function playGameSfx(name: SfxName) {
  if (muted) return
  if (name === 'start') {
    tone(523.25, 0.11, 0.045)
    tone(659.25, 0.13, 0.045, 'sine', 0.09)
    tone(783.99, 0.16, 0.04, 'sine', 0.18)
  } else if (name === 'pause') {
    tone(440, 0.09, 0.035)
    tone(349.23, 0.12, 0.03, 'triangle', 0.08)
  } else if (name === 'resume') {
    tone(392, 0.09, 0.035)
    tone(523.25, 0.12, 0.04, 'triangle', 0.08)
  } else if (name === 'success') {
    tone(523.25, 0.1, 0.045)
    tone(659.25, 0.1, 0.045, 'sine', 0.08)
    tone(783.99, 0.12, 0.045, 'sine', 0.16)
    tone(1046.5, 0.24, 0.04, 'sine', 0.25)
  } else if (name === 'pop') {
    tone(196, 0.08, 0.055, 'square')
    tone(784, 0.12, 0.035, 'sine', 0.04)
  } else if (name === 'charge') {
    tone(659.25, 0.07, 0.025, 'triangle')
  } else if (name === 'fruit') {
    tone(587.33, 0.08, 0.035)
    tone(880, 0.13, 0.04, 'sine', 0.08)
  } else if (name === 'balloon') {
    tone(330, 0.1, 0.035, 'triangle')
    tone(440, 0.12, 0.04, 'triangle', 0.1)
    tone(660, 0.16, 0.035, 'sine', 0.2)
  } else if (name === 'error') {
    tone(220, 0.12, 0.035, 'sawtooth')
    tone(185, 0.16, 0.03, 'sawtooth', 0.11)
  }
}

export function startGameBgm(kind = 'cute') {
  if (muted || bgmTimer) return
  bgmKind = kind
  bgmStep = 0
  bgmTimer = setInterval(() => {
    if (muted) return
    const scale = bgmScales[bgmKind] || bgmScales.cute
    const freq = scale[bgmStep % scale.length]
    tone(freq, 0.18, 0.012, 'sine')
    if (bgmStep % 2 === 0) tone(freq / 2, 0.22, 0.008, 'triangle')
    bgmStep += 1
  }, 520)
}

export function stopGameBgm() {
  if (bgmTimer) {
    clearInterval(bgmTimer)
    bgmTimer = null
  }
}
