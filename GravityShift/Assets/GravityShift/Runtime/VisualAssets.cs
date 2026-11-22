using System;
using UnityEngine;

namespace GravityShift
{
    [Serializable]
    internal sealed class VisualTileSet
    {
        // exposedMask bits are N=1, E=2, S=4, W=8; corners are ordered NE, SE, SW, NW.
        [SerializeField] private Sprite[] edgeMasks = new Sprite[16];
        [SerializeField] private Sprite[] innerCorners = new Sprite[4];

        internal Sprite Edge(int exposedMask) => edgeMasks[exposedMask];
        internal Sprite InnerCorner(int corner) => innerCorners[corner];
        internal bool IsComplete => VisualAssets.HasAll(edgeMasks, 16) && VisualAssets.HasAll(innerCorners, 4);
    }

    public sealed class VisualAssets : ScriptableObject
    {
        [SerializeField] private Sprite[] playerIdleFrames;
        [SerializeField] private Sprite[] playerRunFrames;
        [SerializeField] private Sprite playerRise;
        [SerializeField] private Sprite playerFall;
        [SerializeField] private Sprite[] playerDashFrames;
        [SerializeField] private Sprite[] playerAttackFrames;
        [SerializeField] private Sprite playerVisor;

        [SerializeField] private Sprite[] walkerIdleFrames;
        [SerializeField] private Sprite[] walkerWalkFrames;
        [SerializeField] private Sprite[] anchorIdleFrames;
        [SerializeField] private Sprite[] anchorWalkFrames;
        [SerializeField] private Sprite[] inverterIdleFrames;
        [SerializeField] private Sprite[] inverterWalkFrames;

        [SerializeField] private Sprite enemyPupil;
        [SerializeField] private Sprite anchorRing;
        [SerializeField] private Sprite stunStar;

        [SerializeField] private Sprite researchWingBackground;
        [SerializeField] private Sprite magicTowerBackground;
        [SerializeField] private Sprite orbitalStationBackground;
        [SerializeField] private VisualTileSet researchWingTiles = new VisualTileSet();
        [SerializeField] private VisualTileSet magicTowerTiles = new VisualTileSet();
        [SerializeField] private VisualTileSet orbitalStationTiles = new VisualTileSet();

        [SerializeField] private Sprite crate;
        [SerializeField] private Sprite spikes;
        [SerializeField] private Sprite exitClosed;
        [SerializeField] private Sprite exitOpen;

        [SerializeField] private Sprite gravityBeam;
        [SerializeField] private Sprite gravityArrow;
        [SerializeField] private Sprite inverterWarningRing;
        [SerializeField] private Sprite telekinesisTether;
        [SerializeField] private Sprite[] puffFrames;

        [SerializeField] private Sprite panel9Slice;
        [SerializeField] private Sprite healthFull;
        [SerializeField] private Sprite healthEmpty;
        [SerializeField] private Sprite fieldFull;
        [SerializeField] private Sprite fieldEmpty;
        [SerializeField] private Font hudFont;

        internal Sprite[] PlayerIdleFrames => playerIdleFrames;
        internal Sprite[] PlayerRunFrames => playerRunFrames;
        internal Sprite PlayerRise => playerRise;
        internal Sprite PlayerFall => playerFall;
        internal Sprite[] PlayerDashFrames => playerDashFrames;
        internal Sprite[] PlayerAttackFrames => playerAttackFrames;
        internal Sprite PlayerVisor => playerVisor;

        internal Sprite[] WalkerIdleFrames => walkerIdleFrames;
        internal Sprite[] WalkerWalkFrames => walkerWalkFrames;
        internal Sprite[] AnchorIdleFrames => anchorIdleFrames;
        internal Sprite[] AnchorWalkFrames => anchorWalkFrames;
        internal Sprite[] InverterIdleFrames => inverterIdleFrames;
        internal Sprite[] InverterWalkFrames => inverterWalkFrames;

        internal Sprite EnemyPupil => enemyPupil;
        internal Sprite AnchorRing => anchorRing;
        internal Sprite StunStar => stunStar;

        internal Sprite BackgroundFor(int zone)
        {
            switch (zone)
            {
                case 0: return researchWingBackground;
                case 1: return magicTowerBackground;
                case 2: return orbitalStationBackground;
                default: throw new ArgumentOutOfRangeException(nameof(zone));
            }
        }

        internal VisualTileSet TilesFor(int zone)
        {
            switch (zone)
            {
                case 0: return researchWingTiles;
                case 1: return magicTowerTiles;
                case 2: return orbitalStationTiles;
                default: throw new ArgumentOutOfRangeException(nameof(zone));
            }
        }

        internal Sprite Crate => crate;
        internal Sprite Spikes => spikes;
        internal Sprite ExitClosed => exitClosed;
        internal Sprite ExitOpen => exitOpen;

        internal Sprite GravityBeam => gravityBeam;
        internal Sprite GravityArrow => gravityArrow;
        internal Sprite InverterWarningRing => inverterWarningRing;
        internal Sprite TelekinesisTether => telekinesisTether;
        internal Sprite[] PuffFrames => puffFrames;

        internal Sprite Panel9Slice => panel9Slice;
        internal Sprite HealthFull => healthFull;
        internal Sprite HealthEmpty => healthEmpty;
        internal Sprite FieldFull => fieldFull;
        internal Sprite FieldEmpty => fieldEmpty;
        internal Font HudFont => hudFont;

        internal void RequireComplete()
        {
            if (!HasAll(playerIdleFrames, 4) || !HasAll(playerRunFrames, 6) || playerRise == null || playerFall == null ||
                !HasAll(playerDashFrames, 2) || !HasAll(playerAttackFrames, 4) || playerVisor == null ||
                !HasAll(walkerIdleFrames, 2) || !HasAll(walkerWalkFrames, 6) ||
                !HasAll(anchorIdleFrames, 2) || !HasAll(anchorWalkFrames, 6) ||
                !HasAll(inverterIdleFrames, 2) || !HasAll(inverterWalkFrames, 6) ||
                enemyPupil == null || anchorRing == null || stunStar == null ||
                researchWingBackground == null || magicTowerBackground == null || orbitalStationBackground == null ||
                researchWingTiles == null || !researchWingTiles.IsComplete ||
                magicTowerTiles == null || !magicTowerTiles.IsComplete ||
                orbitalStationTiles == null || !orbitalStationTiles.IsComplete ||
                crate == null || spikes == null || exitClosed == null || exitOpen == null ||
                gravityBeam == null || gravityArrow == null || inverterWarningRing == null ||
                telekinesisTether == null || !HasAll(puffFrames, 4) ||
                panel9Slice == null || healthFull == null || healthEmpty == null ||
                fieldFull == null || fieldEmpty == null || hudFont == null)
                throw new InvalidOperationException($"VisualAssets '{name}' is missing required sprite or font references.");
        }

        internal static bool HasAll(Sprite[] sprites, int expectedCount)
        {
            if (sprites == null || sprites.Length != expectedCount) return false;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] == null) return false;
            return true;
        }
    }
}
