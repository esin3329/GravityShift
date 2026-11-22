using System;
using UnityEngine;

namespace GravityShift
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class GravityShiftGame : MonoBehaviour
    {
        private const float Step = 1f / 60;
        [SerializeField] private Material spriteMaterial;
        [Tooltip("Enter a 32-bit unsigned number to reproduce a run. Leave blank for a random seed.")]
        [SerializeField] private string initialSeed;
        [SerializeField] private VisualAssets visualAssets;
        [SerializeField] private AudioAssets audioAssets;

        private Camera gameCamera;
        private GameState state;
        private RoomView room;
        private HudView hud;
        private Controls controls;
        private Sfx sfx;
        private float accumulator;

        private void Awake()
        {
            if (spriteMaterial == null) throw new InvalidOperationException("Assign the unlit sprite material on GravityShiftGame.");
            if (visualAssets == null) throw new InvalidOperationException("Assign VisualAssets on GravityShiftGame.");
            if (audioAssets == null) throw new InvalidOperationException("Assign AudioAssets on GravityShiftGame.");
            visualAssets.RequireComplete();

            gameCamera = GetComponent<Camera>();
            gameCamera.orthographic = true;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = Color.black;
            gameCamera.transform.SetPositionAndRotation(new Vector3(RoomMap.Width * 0.5f, -RoomMap.Height * 0.5f, -10), Quaternion.identity);

            // Keep rendering in world coordinates even while the room root shakes.
            room = new RoomView(spriteMaterial, visualAssets);
            hud = new HudView(transform, visualAssets);
            state = new GameState(ReadSeed());
            sfx = new Sfx(gameObject, audioAssets);
            state.BurstRequested += room.Burst;
            state.NoteRequested += hud.Note;
            state.SfxRequested += sfx.Play;
            sfx.SetZone(state.Room.Zone);
        }

        private uint? ReadSeed()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-seed");
            if (index >= 0 && index + 1 < args.Length && uint.TryParse(args[index + 1], out uint custom))
                return custom;
            return uint.TryParse(initialSeed, out uint seed) ? seed : (uint?)null;
        }

        private void Update()
        {
            gameCamera.orthographicSize = Mathf.Max(RoomMap.Height * 0.5f, RoomMap.Width * 0.5f / gameCamera.aspect);
            controls.Poll(gameCamera);
            if (state.Paused) controls.DiscardGameplayPresses();

            float frame = Mathf.Min(Time.deltaTime, 0.1f);
            accumulator += frame;
            while (accumulator >= Step)
            {
                bool paused = state.Paused;
                bool hitstop = state.Hitstop > 0f;
                int roomVersion = state.RoomVersion;
                state.Tick(controls, Step);
                controls.ConsumePresses(ActionButton.Pause | ActionButton.Retry | ActionButton.NewRun);
                if (paused || state.Paused || roomVersion != state.RoomVersion)
                    controls.DiscardGameplayPresses();
                else if (!hitstop)
                    controls.ConsumePresses();
                accumulator -= Step;
            }
            sfx.SetZone(state.Room.Zone);

            room.Draw(state, controls, frame);
            hud.Draw(state, frame);
            Cursor.visible = state.Paused || !Application.isFocused || !controls.PointerInRoom;
        }

        private void OnDisable() => Cursor.visible = true;

        private void OnDestroy()
        {
            if (state != null)
            {
                if (room != null) state.BurstRequested -= room.Burst;
                if (hud != null) state.NoteRequested -= hud.Note;
                if (sfx != null) state.SfxRequested -= sfx.Play;
            }
            sfx?.Dispose();
            room?.Dispose();
            hud?.Dispose();
        }
    }
}
