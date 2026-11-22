using System;
using UnityEngine;

namespace GravityShift
{
    internal sealed class AudioAssets : ScriptableObject
    {
        [SerializeField] private AudioClip researchWing;
        [SerializeField] private AudioClip magicTower;
        [SerializeField] private AudioClip orbitalStation;
        [SerializeField] private AudioClip clear;
        [SerializeField] private AudioClip dash;
        [SerializeField] private AudioClip death;
        [SerializeField] private AudioClip exitOpen;
        [SerializeField] private AudioClip fieldPlace;
        [SerializeField] private AudioClip fieldRemove;
        [SerializeField] private AudioClip fieldReverseAll;
        [SerializeField] private AudioClip grab;
        [SerializeField] private AudioClip gravityFlip;
        [SerializeField] private AudioClip impact;
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip land;
        [SerializeField] private AudioClip rotationWarning;
        [SerializeField] private AudioClip swing;
        [SerializeField] private AudioClip throwClip;

        public AudioClip MusicFor(int zone) => zone switch
        {
            0 => researchWing,
            1 => magicTower,
            2 => orbitalStation,
            _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "No music for this zone.")
        };

        public AudioClip ClipFor(SfxKind kind) => kind switch
        {
            SfxKind.Clear => clear,
            SfxKind.Dash => dash,
            SfxKind.Death => death,
            SfxKind.ExitOpen => exitOpen,
            SfxKind.FieldPlace => fieldPlace,
            SfxKind.FieldRemove => fieldRemove,
            SfxKind.FieldReverseAll => fieldReverseAll,
            SfxKind.Grab => grab,
            SfxKind.GravityFlip => gravityFlip,
            SfxKind.Impact => impact,
            SfxKind.Jump => jump,
            SfxKind.Land => land,
            SfxKind.RotationWarning => rotationWarning,
            SfxKind.Swing => swing,
            SfxKind.Throw => throwClip,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No authored clip for this cue.")
        };

        public void RequireClips()
        {
            Require(researchWing, nameof(researchWing));
            Require(magicTower, nameof(magicTower));
            Require(orbitalStation, nameof(orbitalStation));
            Require(clear, nameof(clear));
            Require(dash, nameof(dash));
            Require(death, nameof(death));
            Require(exitOpen, nameof(exitOpen));
            Require(fieldPlace, nameof(fieldPlace));
            Require(fieldRemove, nameof(fieldRemove));
            Require(fieldReverseAll, nameof(fieldReverseAll));
            Require(grab, nameof(grab));
            Require(gravityFlip, nameof(gravityFlip));
            Require(impact, nameof(impact));
            Require(jump, nameof(jump));
            Require(land, nameof(land));
            Require(rotationWarning, nameof(rotationWarning));
            Require(swing, nameof(swing));
            Require(throwClip, nameof(throwClip));
        }

        private void Require(AudioClip clip, string field)
        {
            if (clip == null) throw new InvalidOperationException($"AudioAssets '{name}' is missing its '{field}' clip.");
        }
    }
}
