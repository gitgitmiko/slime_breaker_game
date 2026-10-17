using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SlimeBreaker
{
    /// <summary>
    /// Plays crossfaded music (one track per biome plus a boss track), a looping biome ambience bed, a weather
    /// ambience layer, and throttled, pitch-varied one-shot effects through a small pool of AudioSources.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Range(0f, 0.5f)] public float pitchJitter = 0.06f;
            /// <summary>Minimum seconds between two plays of the same id, so swarms do not stack into noise.</summary>
            public float minInterval = 0.03f;
            /// <summary>How strongly rapid repeats are softened (0 = never): each recent play lowers the next one's volume.</summary>
            public float crowdFalloff;
            [NonSerialized] public float lastPlayed = -10f;
            [NonSerialized] public float recent;
        }

        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.42f;
        /// <summary>Indexed by <see cref="Biome"/>; a missing entry falls back to <see cref="music"/>.</summary>
        public AudioClip[] biomeMusic;
        public AudioClip bossMusic;
        /// <summary>Looping environment bed indexed by <see cref="Biome"/>.</summary>
        public AudioClip[] biomeAmbience;
        public AudioClip nightAmbience;
        [Range(0f, 1f)] public float ambienceVolume = 0.55f;
        public float crossfadeSeconds = 1.6f;
        public Entry[] entries;
        public int voices = 10;

        AudioSource musicSource;
        AudioSource fadingSource;
        AudioSource bedSource;
        AudioSource[] pool;
        float musicDuck = 1f;
        float fade = 1f;
        AudioClip themeMusic;
        bool bossActive;
        int nextVoice;
        Dictionary<string, Entry> lookup;

        void Awake()
        {
            Instance = this;
            musicSource = LoopSource(musicVolume);
            fadingSource = LoopSource(0f);
            bedSource = LoopSource(ambienceVolume);

            pool = new AudioSource[Mathf.Max(2, voices)];
            for (int i = 0; i < pool.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                pool[i] = source;
            }

            lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id))
                        continue;
                    lookup[entry.id] = entry;
                }
            }

            // Scenes without a theme (main menu) open on the first bed; arenas replace it in SetTheme.
            if (biomeAmbience != null && biomeAmbience.Length > 0)
                SetBed(biomeAmbience[0]);
            if (music != null)
            {
                themeMusic = music;
                musicSource.clip = music;
                musicSource.Play();
            }
        }

        AudioSource LoopSource(float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            return source;
        }

        void Update()
        {
            if (fade >= 1f)
                return;
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, crossfadeSeconds));
            // Equal-power crossfade so the overall level does not dip mid-transition.
            float target = musicVolume * musicDuck;
            musicSource.volume = target * Mathf.Sin(fade * Mathf.PI * 0.5f);
            fadingSource.volume = target * Mathf.Cos(fade * Mathf.PI * 0.5f);
            if (fade >= 1f)
            {
                fadingSource.Stop();
                fadingSource.clip = null;
            }
        }

        public AudioClip CurrentMusic => musicSource != null ? musicSource.clip : null;
        public AudioClip CurrentBed => bedSource != null && bedSource.isPlaying ? bedSource.clip : null;
        public bool BossMusicActive => bossActive;

        /// <summary>Biome track and environment bed for an arena theme (boss music, when active, keeps playing).</summary>
        public void SetTheme(ArenaTheme theme)
        {
            int index = (int)theme.biome;
            AudioClip track = biomeMusic != null && index >= 0 && index < biomeMusic.Length ? biomeMusic[index] : null;
            themeMusic = track != null ? track : music;
            if (!bossActive)
                CrossfadeTo(themeMusic);

            AudioClip bed = biomeAmbience != null && index >= 0 && index < biomeAmbience.Length ? biomeAmbience[index] : null;
            if (theme.IsNight && nightAmbience != null && (theme.biome == Biome.Forest || theme.biome == Biome.Royal))
                bed = nightAmbience;
            SetBed(bed);
        }

        /// <summary>Switches to the boss track while a boss fight runs, and back to the biome track afterwards.</summary>
        public void SetBossMusic(bool active)
        {
            bossActive = active && bossMusic != null;
            CrossfadeTo(bossActive ? bossMusic : themeMusic);
        }

        void CrossfadeTo(AudioClip clip)
        {
            if (musicSource == null || clip == null)
                return;
            if (musicSource.clip == clip && musicSource.isPlaying)
                return;
            if (musicSource.clip == null || !musicSource.isPlaying || crossfadeSeconds <= 0f)
            {
                musicSource.clip = clip;
                musicSource.volume = musicVolume * musicDuck;
                musicSource.Play();
                fade = 1f;
                return;
            }
            // Swap roles: the current track fades out on the spare source while the new one fades in.
            var old = musicSource;
            musicSource = fadingSource;
            fadingSource = old;
            musicSource.clip = clip;
            musicSource.volume = 0f;
            musicSource.Play();
            fade = 0f;
        }

        void SetBed(AudioClip clip)
        {
            if (bedSource == null)
                return;
            if (clip == null)
            {
                bedSource.Stop();
                bedSource.clip = null;
                return;
            }
            if (bedSource.clip == clip && bedSource.isPlaying)
                return;
            bedSource.clip = clip;
            bedSource.volume = ambienceVolume;
            // Start at a random point so floors sharing a biome do not open with the identical bird call.
            bedSource.time = Random.Range(0f, clip.length * 0.9f);
            bedSource.Play();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool Has(string id) => lookup != null && id != null && lookup.ContainsKey(id);

        public void Play(string id)
        {
            Play(id, 1f);
        }

        public void Play(string id, float volumeScale)
        {
            if (lookup == null || id == null)
                return;
            if (!lookup.TryGetValue(id, out var entry) || entry.clip == null)
                return;
            float now = Time.unscaledTime;
            float since = now - entry.lastPlayed;
            if (since < entry.minInterval)
                return;
            // "recent" decays with a 0.35 s half-life; a burst of 20 orb pickups settles into a quiet patter.
            entry.recent = entry.recent * Mathf.Pow(0.5f, since / 0.35f) + 1f;
            entry.lastPlayed = now;
            if (entry.crowdFalloff > 0f)
                volumeScale /= 1f + (entry.recent - 1f) * entry.crowdFalloff;

            var source = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.PlayOneShot(entry.clip, Mathf.Clamp01(entry.volume * volumeScale));
        }

        public void SetMusic(AudioClip clip)
        {
            if (musicSource == null)
                return;
            if (clip == null)
            {
                musicSource.Stop();
                return;
            }
            themeMusic = clip;
            CrossfadeTo(clip);
        }

        public void DuckMusic(float volume01)
        {
            musicDuck = Mathf.Clamp01(volume01);
            if (musicSource != null && fade >= 1f)
                musicSource.volume = musicVolume * musicDuck;
        }

        /// <summary>Loops a weather ambience (rain, wind) from the entry table; null stops the loop.</summary>
        public void SetAmbience(string id)
        {
            if (ambienceSource == null)
            {
                ambienceSource = gameObject.AddComponent<AudioSource>();
                ambienceSource.loop = true;
                ambienceSource.playOnAwake = false;
                ambienceSource.spatialBlend = 0f;
            }
            if (string.IsNullOrEmpty(id) || lookup == null || !lookup.TryGetValue(id, out var entry) || entry.clip == null)
            {
                ambienceSource.Stop();
                ambienceSource.clip = null;
                return;
            }
            if (ambienceSource.clip == entry.clip && ambienceSource.isPlaying)
                return;
            ambienceSource.clip = entry.clip;
            ambienceSource.volume = entry.volume;
            ambienceSource.Play();
        }

        AudioSource ambienceSource;
    }
}
