﻿using NLog;

namespace WorldServer.World.Battlefronts.Apocalypse
{
    public class SiegeTracker
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public byte MaxNumberSiege { get; set; }
        public byte CurrentNumberSiege { get; set; }
        public SiegeType Type { get; set; }

        public override string ToString()
        {
            return $"{Type} {CurrentNumberSiege}/{MaxNumberSiege}";
        }

        public bool CanDeploy()
        {
            _logger.Debug($"{ToString()}");
            return CurrentNumberSiege < MaxNumberSiege;
        }

        public void Increment()
        {
            CurrentNumberSiege++;
            if (CurrentNumberSiege > MaxNumberSiege)
                _logger.Warn($"Number of Siege now exceeds maximum!");

        }
        public void Decrement()
        {
            CurrentNumberSiege--;
            if (CurrentNumberSiege < 0)
                _logger.Warn($"Number of Siege now less than zero!");

        }

    }
}