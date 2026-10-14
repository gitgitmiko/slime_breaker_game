using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }
        public AudioClip music;
        public AudioClip hit;
        public AudioClip playerHit;
        public AudioClip dash;
        public AudioClip pickup;
        public AudioClip levelUp;
        public AudioClip death;
        public AudioClip bossWarn;
        public AudioClip bossSlam;
        public AudioClip ui;
        public AudioClip victory;
        public AudioClip defeat;

        AudioSource musicSource;
        AudioSource effectSource;
        Dictionary<string, AudioClip> lookup;

        void Awake()
        {
            Instance = this;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = 0.35f;
            musicSource.playOnAwake = false;
            effectSource = gameObject.AddComponent<AudioSource>();
            effectSource.playOnAwake = false;
            lookup = new Dictionary<string, AudioClip>
            {
                { "hit", hit },
                { "playerhit", playerHit },
                { "dash", dash },
                { "pickup", pickup },
                { "levelup", levelUp },
                { "death", death },
                { "bosswarn", bossWarn },
                { "slam", bossSlam },
                { "ui", ui },
                { "victory", victory },
                { "defeat", defeat }
            };
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

        public void Play(string id)
        {
            if (lookup == null || id == null)
                return;
            if (!lookup.TryGetValue(id, out var clip) || clip == null)
                return;
            effectSource.PlayOneShot(clip, id == "hit" ? 0.45f : 0.8f);
        }
    }
}
