using System;
using System.Collections.Generic;
using System.IO;
using ChimeralTheater.Films.Avatars;
using ChimeralTheater.Films.Props;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeralTheater.UI
{
    /// <summary>
    /// Drives the Asset Inventory tab on the VR Dashboard: indexes all converted .chavi avatars and
    /// .chprop props in the AssetInventory/ directory, displays their metadata, and allows deletion.
    /// Avatar rows toggle Wear/Unwear; prop rows toggle Spawn/Despawn.
    /// </summary>
    public class AssetInventoryPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text countText;
        [SerializeField] private Button refreshButton;
        [SerializeField] private RectTransform inventoryListContent;
        [SerializeField] private GameObject inventoryRowTemplate;

        private readonly List<GameObject> activeRows = new List<GameObject>();

        private static readonly Color RowColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        private static readonly Color WearButtonColor = new Color(0.2f, 0.5f, 0.3f, 0.95f);
        private static readonly Color WornButtonColor = new Color(0.85f, 0.6f, 0.15f, 0.95f);
        private static readonly Color DeleteButtonColor = new Color(0.8f, 0.25f, 0.25f, 0.9f);

        public RectTransform InventoryListContent { get => inventoryListContent; set => inventoryListContent = value; }
        public GameObject InventoryRowTemplate { get => inventoryRowTemplate; set => inventoryRowTemplate = value; }
        public Text CountText { get => countText; set => countText = value; }
        public Button RefreshButton { get => refreshButton; set => refreshButton = value; }

        private void Start()
        {
            AssetStorePaths.EnsureDirectoriesExist();
            refreshButton?.onClick.RemoveAllListeners();
            refreshButton?.onClick.AddListener(Refresh);
            Refresh();
        }

        public void Refresh()
        {
            AssetStorePaths.EnsureDirectoriesExist();
            ClearRows();

            string[] avatarFiles = AssetStorePaths.GetInventoryAvatars();
            string[] propFiles = AssetStorePaths.GetInventoryProps();

            if (countText != null)
                countText.text = $"{avatarFiles.Length} Avatar{(avatarFiles.Length == 1 ? "" : "s")}, " +
                                 $"{propFiles.Length} Prop{(propFiles.Length == 1 ? "" : "s")}";

            foreach (string avatarPath in avatarFiles)
            {
                var fi = new FileInfo(avatarPath);
                string avatarName = Path.GetFileNameWithoutExtension(fi.Name);
                double kb = fi.Length / 1024.0;

                string prefix = AvatarWearState.IsWorn(avatarPath) ? "★" : "👤";
                CreateRow($"{prefix} {avatarName} ({kb:0.0} KB)", avatarPath, isProp: false);
            }

            foreach (string propPath in propFiles)
            {
                var fi = new FileInfo(propPath);
                string propName = Path.GetFileNameWithoutExtension(fi.Name);
                double kb = fi.Length / 1024.0;

                string prefix = PropRegistry.IsSpawned(propPath) ? "★" : "📦";
                CreateRow($"{prefix} {propName} ({kb:0.0} KB)", propPath, isProp: true);
            }
        }

        private GameObject CreateRow(string labelText, string avatarPath, bool isProp)
        {
            // The row template is Label | Wear | Delete.
            GameObject row = Instantiate(inventoryRowTemplate, inventoryListContent);
            row.name = "Inventory Row";
            row.SetActive(true);

            var label = row.GetComponentInChildren<Text>();
            if (label != null)
                label.text = labelText;

            var img = row.GetComponent<Image>();
            if (img != null)
                img.color = RowColor;

            // Wire delete button
            Transform delTransform = row.transform.Find("Delete");
            if (delTransform != null)
            {
                var delBtn = delTransform.GetComponent<Button>();
                if (delBtn != null)
                {
                    string capturedPath = avatarPath;
                    delBtn.onClick.AddListener(() =>
                    {
                        if (isProp)
                        {
                            PropRegistry.Despawn(capturedPath);
                            PropConversionPipeline.DeleteProp(capturedPath);
                        }
                        else
                        {
                            if (AvatarWearState.IsWorn(capturedPath))
                                AvatarWearState.Worn = "";
                            AvatarConversionPipeline.DeleteAvatar(capturedPath);
                        }
                        Refresh();
                    });
                }
            }

            BindWearButton(row.transform.Find("Wear"), avatarPath, isProp);

            activeRows.Add(row);
            return row;
        }

        /// <summary>Binds the row's Wear/Unwear toggle for an avatar, or Spawn/Despawn for a prop.</summary>
        private void BindWearButton(Transform wearT, string avatarPath, bool isProp)
        {
            bool active = isProp ? PropRegistry.IsSpawned(avatarPath) : AvatarWearState.IsWorn(avatarPath);
            wearT.GetComponent<Image>().color = active ? WornButtonColor : WearButtonColor;
            var label = wearT.GetComponentInChildren<Text>();
            if (label != null)
                label.text = isProp ? (active ? "Despawn" : "Spawn") : (active ? "Unwear" : "Wear");

            var btn = wearT.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            string captured = avatarPath;
            btn.onClick.AddListener(() =>
            {
                if (isProp)
                {
                    if (PropRegistry.IsSpawned(captured))
                        PropRegistry.Despawn(captured);
                    else
                        PropRegistry.Spawn(captured, RigSpawnPoint());
                }
                else
                {
                    AvatarWearState.Worn = AvatarWearState.IsWorn(captured) ? "" : captured;
                }
                Refresh();
            });
        }

        /// <summary>Distance in front of the player, on the floor, where spawned props drop. Spawning exactly on the
        /// rig would put the prop inside the player's body capsule, which would shove it away.</summary>
        private const float SpawnDistance = 0.6f;

        /// <summary>The floor point just in front of the player's rig: under the headset, pushed forward
        /// along where they are looking.</summary>
        private static Vector3 RigSpawnPoint()
        {
            Camera hmd = Camera.main;
            GameObject body = GameObject.Find("Player Body");
            if (hmd == null)
                return body != null ? body.transform.position : Vector3.zero;

            Vector3 forward = hmd.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;

            Vector3 point = hmd.transform.position + forward * SpawnDistance;
            point.y = body != null ? body.transform.position.y : 0f;
            return point;
        }

        private void ClearRows()
        {
            foreach (var row in activeRows)
            {
                if (row != null)
                    Destroy(row);
            }
            activeRows.Clear();
        }
    }
}
