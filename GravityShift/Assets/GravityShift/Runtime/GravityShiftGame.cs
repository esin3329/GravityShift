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

        private Camera gameCamera;
        private GameState state;
        private RoomView room;
        private HudView hud;
        private Controls controls;
        private float accumulator;

        private void Awake()
        {
            gameCamera = GetComponent<Camera>();
            gameCamera.orthographic = true;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = Color.black;
            gameCamera.transform.SetPositionAndRotation(new Vector3(RoomMap.Width * 0.5f, -RoomMap.Height * 0.5f, -10), Quaternion.identity);

            // Keep rendering in world coordinates even while the room root shakes.
            room = new RoomView(spriteMaterial);
            hud = new HudView(transform);
            state = new GameState(ReadSeed());
            state.BurstRequested += room.Burst;
            state.NoteRequested += hud.Note;
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
            float frame = Mathf.Min(Time.deltaTime, 0.1f);
            accumulator += frame;
            while (accumulator >= Step)
            {
                state.Tick(controls, Step);
                controls.ConsumePresses();
                accumulator -= Step;
            }
            room.Draw(state, controls, frame);
            hud.Draw(state, frame);
            Cursor.visible = !Application.isFocused || !controls.PointerInRoom;
        }

        private void OnDisable() => Cursor.visible = true;

        private void OnDestroy()
        {
            if (state != null)
            {
                state.BurstRequested -= room.Burst;
                state.NoteRequested -= hud.Note;
            }
            room?.Dispose();
            hud?.Dispose();
        }
    }
}
