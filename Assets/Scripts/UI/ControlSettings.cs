namespace SlimeBreaker
{
    /// <summary>Touch control preferences kept in the meta save. The joystick is fixed unless the player turns on free mode.</summary>
    public static class ControlSettings
    {
        static bool loaded;
        static bool freeJoystick;
        static bool invertCamera;
        static bool invertLoaded;

        /// <summary>Normal (false, default): drag right turns the view right and drag up looks up. Inverted: both flipped, the world follows the finger.</summary>
        public static bool InvertCamera
        {
            get
            {
                if (!invertLoaded)
                {
                    invertCamera = SaveService.Load().invertCamera;
                    invertLoaded = true;
                }
                return invertCamera;
            }
            set
            {
                invertCamera = value;
                invertLoaded = true;
                var save = SaveService.Load();
                save.invertCamera = value;
                SaveService.Save(save);
            }
        }

        public static bool FreeJoystick
        {
            get
            {
                if (!loaded)
                {
                    freeJoystick = SaveService.Load().freeJoystick;
                    loaded = true;
                }
                return freeJoystick;
            }
            set
            {
                freeJoystick = value;
                loaded = true;
                var save = SaveService.Load();
                save.freeJoystick = value;
                SaveService.Save(save);
            }
        }

        /// <summary>Forgets the cached value so the next read comes from disk (tests, save resets).</summary>
        public static void Reload()
        {
            loaded = false;
            invertLoaded = false;
        }
    }
}
