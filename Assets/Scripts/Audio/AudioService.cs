using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Plays music and throttled, pitch-varied one-shot effects through a small pool of AudioSources.</summary>
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
            [NonSerialized] public float lastPlayed = -10f;
        }

        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.42f;
        public Entry[] entries;
        public int voices = 10;

        AudioSource musicSource;
        AudioSource[] pool;
        int nextVoice;
        Dictionary<string, Entry> lookup;

        void Awake()
        {
            Instance = this;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

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

            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }
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
            if (now - entry.lastPlayed < entry.minInterval)
                return;
            entry.lastPlayed = now;

            var source = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.PlayOneShot(entry.clip, Mathf.Clamp01(entry.volume * volumeScale));
        }

        public void SetMusic(AudioClip clip)
        {
            if (musicSource == null)
                return;
            if (musicSource.clip == clip && musicSource.isPlaying)
                return;
            musicSource.clip = clip;
            if (clip != null)
                musicSource.Play();
            else
                musicSource.Stop();
        }

        public void DuckMusic(float volume01)
        {
            if (musicSource != null)
                musicSource.volume = musicVolume * Mathf.Clamp01(volume01);
        }
    }
}
