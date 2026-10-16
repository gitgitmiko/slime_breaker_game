namespace SlimeBreaker
{
    /// <summary>Touch control preferences kept in the meta save. The joystick is fixed unless the player turns on free mode.</summary>
    public static class ControlSettings
    {
        static bool loaded;
        static bool freeJoystick;

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
        }
    }
}
