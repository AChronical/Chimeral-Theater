using ChimeralTheater.Films;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChimeralTheater.Props
{
    /// <summary>
    /// Feeds the controllers' grip buttons to the <see cref="FilmDirector"/>, which grabs and releases
    /// props with them and records them into acts. Lives on the director's object in the scene and
    /// reads the LeftGrip / RightGrip actions from the input asset.
    /// </summary>
    public class PropGripInput : MonoBehaviour
    {
        const string MapName = "Chimeral Theater";

        [SerializeField] private FilmDirector director;
        [SerializeField] private InputActionAsset inputActions;

        InputAction leftGrip;
        InputAction rightGrip;

        public FilmDirector Director { get => director; set => director = value; }
        public InputActionAsset InputActions { get => inputActions; set => inputActions = value; }

        void Awake()
        {
            if (director == null)
                director = GetComponent<FilmDirector>();

            InputActionMap map = inputActions != null ? inputActions.FindActionMap(MapName, false) : null;
            leftGrip = map?.FindAction("LeftGrip", false);
            rightGrip = map?.FindAction("RightGrip", false);
            if (leftGrip == null || rightGrip == null)
                Debug.LogWarning("[PropGripInput] LeftGrip / RightGrip actions not found; prop grabbing is off.");
        }

        void OnEnable()
        {
            leftGrip?.Enable();
            rightGrip?.Enable();
        }

        void Update()
        {
            if (director == null)
                return;

            director.SetLiveGrip(
                leftGrip != null && leftGrip.IsPressed(),
                rightGrip != null && rightGrip.IsPressed());
        }
    }
}
