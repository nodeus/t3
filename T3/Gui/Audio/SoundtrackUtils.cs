﻿using System.Linq;
using Core.Audio;
using T3.Core.Animation;
using T3.Core.Operator;
using T3.Gui.Graph;

namespace t3.Gui.Audio
{
    public static class SoundtrackUtils
    {
        public static void UpdateMainSoundtrack()
        {
            var primaryGraphWindow = GraphWindow.GetPrimaryGraphWindow();
            if (primaryGraphWindow == null)
                return;

            var composition = primaryGraphWindow.GraphCanvas.CompositionOp;

            if (TryFindingSoundtrack(composition, out var soundtrack))
            {
                Playback.Current.Bpm = soundtrack.Bpm;
                AudioEngine.UseAudioClip(soundtrack, Playback.Current.TimeInSecs);
            } 
        }

        public static bool TryFindingSoundtrack(Instance composition, out AudioClip soundtrack)
        {
            //soundtrackSymbol = null;
            while (true)
            {
                var soundtrackSymbol = composition.Symbol;
                soundtrack = soundtrackSymbol.AudioClips.SingleOrDefault(ac => ac.IsSoundtrack);
                if (soundtrack != null)
                {
                    return true;
                }

                if (composition.Parent == null)
                {
                    //soundtrackSymbol = null;
                    //Log.Debug("no soundtrack found");
                    return false;
                }

                composition = composition.Parent;
            }
        }
    }
}