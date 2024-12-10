using MagicLeap.Android;
using MagicLeap.OpenXR.Features.LocalizationMaps;
using System;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.NativeTypes;

namespace MagicLeap.SpatialMeshSerializer
{
    public static class MeshLocalizer
    {
        private static MagicLeapLocalizationMapFeature _localizationMapFeature = null;

        /// <summary>Current Space Origin position/rotation in Unity's coordinates.</summary>
        public static Pose currentOrigin { get; private set; }
        /// <summary>GUID for the space we are currently localized into.</summary>
        public static string currentId { get; private set; } = "";
        /// <summary>Name provided for the space we are currently localized into.</summary>
        public static string currentMapName { get; private set; } = "None";
        /// <summary>Current localization status.</summary>
        public static LocalizationMapState state = LocalizationMapState.NotLocalized;

        /// <summary>Event fired when the headset's localization status changes.</summary>
        public static event Action onLocalizationStatusChanged;

        /// <summary>Transform representing the space origin.</summary>
        public static Transform spaceOrigin { get; private set; }

        public static bool initialized { get; private set; }

        /// <summary>Initialization method to enable localization callbacks.</summary>
        /// <remarks>Must be called before this API can be used.</remarks>
        public static void Initialize()
        {
            if(initialized) return;

            Permissions.RequestPermission(Permissions.SpaceImportExport);
            Permissions.RequestPermission(Permissions.SpaceManager, OnPermissionGranted);
        }

        /// <summary>
        /// Gets the local position/rotation offset <see cref="Pose"/> of this <paramref name="transform"/> to the space origin.
        /// </summary>
        /// <param name="transform">Transform to get the local position/rotation offset from.</param>
        /// <param name="offset">Local position/rotation offset of the <paramref name="transform"/> to the space origin.</param>
        /// <returns><see langword="true"/> if the device is localized to a space, <see langword="false"/> otherwise.</returns>
        public static bool TryGetOffsetPose(Transform transform, out Pose offset)
        {
            offset = default;

            if (state != LocalizationMapState.Localized)
                return false;

            var oldParent = transform.parent;

            transform.SetParent(spaceOrigin);
            offset = new Pose(transform.localPosition, transform.localRotation);
            transform.SetParent(oldParent);

            return true;
        }

        /// <summary>
        /// Localizes this transform into the current space with the provided local position/rotation <see cref="Pose"/> <paramref name="offset"/>.
        /// </summary>
        /// <param name="transform">Transform to localize into the current space.</param>
        /// <param name="offset">Local position/rotation offset from the space origin to apply.</param>
        /// <returns><see langword="true"/> if the device is localized to a space, <see langword="false"/> otherwise.</returns>
        public static bool TryLocalizeTransformToSpace(Transform transform, Pose offset)
        {
            if (state != LocalizationMapState.Localized)
            {
#if UNITY_EDITOR
                transform.SetPositionAndRotation(offset.position, offset.rotation);
#endif
                return false;
            }

            transform.SetParent(spaceOrigin);
            transform.SetLocalPositionAndRotation(offset.position, offset.rotation);

            return true;
        }

        /// <summary>
        /// Gets the current localized space UUID if the user is localized.
        /// </summary>
        /// <param name="id">Current localed space UUID from the Spaces API.</param>
        /// <returns><see langword="true"/> if the device is localized to a space, <see langword="false"/> otherwise.</returns>
        public static bool TryGetSpaceUUID(out string id)
        {
            id = "none";

            if (state != LocalizationMapState.Localized)
                return false;

            id = currentId;

            return true;
        }

        private static void OnPermissionGranted(string permission)
        {
            initialized = TrySubscribeToLocalizationChanges();
        }

        private static bool TrySubscribeToLocalizationChanges()
        {
            _localizationMapFeature = OpenXRSettings.Instance.GetFeature<MagicLeapLocalizationMapFeature>();

            if (_localizationMapFeature == null || !_localizationMapFeature.enabled)
            {
                Debug.LogError("Magic Leap Localization Map Feature does not exist or is disabled.");
                return false;
            }

            var result = _localizationMapFeature.EnableLocalizationEvents(true);
            if (result != XrResult.Success)
            {
                Debug.LogError($"Failed to enable localization events with result: {result}");
                return false;
            }

            spaceOrigin = new GameObject("Space Origin").transform;
            MagicLeapLocalizationMapFeature.OnLocalizationChangedEvent += OnLocalizationChanged;
            return true;
        }

        private static void OnLocalizationChanged(LocalizationEventData data)
        {
            currentMapName = data.State == LocalizationMapState.Localized ? data.Map.Name : "None";

            if (data.State != LocalizationMapState.Localized)
                return;

            state = data.State;
            currentOrigin = _localizationMapFeature.GetMapOrigin();
            currentId = data.Map.MapUUID;
            spaceOrigin.SetPositionAndRotation(currentOrigin.position, currentOrigin.rotation);

            onLocalizationStatusChanged?.Invoke();
        }

        public static bool TryGetMapData(string id, out byte[] data)
        {
            var result = _localizationMapFeature.ExportLocalizationMap(currentId, out data);

            return result == XrResult.Success;
        }

        public static bool TryImportMapData(byte[] data, out string mapId)
        {
            var result = _localizationMapFeature.ImportLocalizationMap(data, out mapId);

            return result == XrResult.Success;
        }

        public static bool TryLocalizeIntoSpace(string id)
        {
            var result = _localizationMapFeature.RequestMapLocalization(id);

            return result == XrResult.Success;
        }
    }
}