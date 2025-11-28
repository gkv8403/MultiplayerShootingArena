using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class MapLoaderManager : MonoBehaviour
{
    [Header("Addressable Map")]
    public string addressableMapKey = "map_white";

    [Header("AssetBundle Map")]
    public string bundleURL = "https://github.com/gkv8403/MultiplayerShootingArena/raw/refs/heads/main/Assets/AssetBundles/mapbundle";
    public string bundleAssetName = "Arena_color";

    [Header("UI")]
    public Button assetLoadBtn;
    public TextMeshProUGUI statusText;

    private GameObject currentMap;
    private bool isLoading = false;

    private void Start()
    {
        Debug.Log("[MapLoader] Starting Addressable map load...");
        StartCoroutine(LoadAddressableMap());

        if (assetLoadBtn != null)
            assetLoadBtn.onClick.AddListener(OnDownloadColorMapButton);
    }

    // ===== LOAD ADDRESSABLE MAP (Default) =====
    private IEnumerator LoadAddressableMap()
    {
        isLoading = true;
        UpdateStatus("Loading addressable map...");

        var handle = Addressables.LoadAssetAsync<GameObject>(addressableMapKey);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            currentMap = Instantiate(handle.Result, Vector3.zero, Quaternion.identity);
            Debug.Log("✓ Addressable map loaded: " + addressableMapKey);
            UpdateStatus("Addressable map loaded!");
            Addressables.Release(handle);
        }
        else
        {
            Debug.LogError("Failed to load Addressable map: " + handle.OperationException);
            UpdateStatus("Addressable load failed!");
            Addressables.Release(handle);
        }

        isLoading = false;
    }

    // ===== LOAD ASSET BUNDLE MAP (Button Click) =====
    public void OnDownloadColorMapButton()
    {
        if (isLoading) return;
        StartCoroutine(LoadAssetBundleMap());
    }

    private IEnumerator LoadAssetBundleMap()
    {
        isLoading = true;
        UpdateStatus("Downloading AssetBundle...");
        if (assetLoadBtn != null) assetLoadBtn.interactable = false;

        using (UnityWebRequest uwr = UnityWebRequest.Get(bundleURL))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Bundle download failed: " + uwr.error);
                UpdateStatus("Bundle download failed!");
                isLoading = false;
                if (assetLoadBtn != null) assetLoadBtn.interactable = true;
                yield break;
            }

            byte[] bundleData = uwr.downloadHandler.data;
            Debug.Log($"Downloaded {bundleData.Length} bytes");

            var abRequest = AssetBundle.LoadFromMemoryAsync(bundleData);
            yield return abRequest;

            if (abRequest.assetBundle == null)
            {
                Debug.LogError("Failed to load AssetBundle from memory");
                UpdateStatus("Bundle load failed!");
                isLoading = false;
                if (assetLoadBtn != null) assetLoadBtn.interactable = true;
                yield break;
            }

            var prefab = abRequest.assetBundle.LoadAsset<GameObject>(bundleAssetName);
            if (prefab == null)
            {
                Debug.LogError($"Asset '{bundleAssetName}' not found in bundle");
                UpdateStatus("Asset not found!");
                abRequest.assetBundle.Unload(false);
                isLoading = false;
                if (assetLoadBtn != null) assetLoadBtn.interactable = true;
                yield break;
            }

            if (currentMap != null)
            {
                Destroy(currentMap);
            }

            currentMap = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            Debug.Log("✓ AssetBundle map loaded: " + bundleAssetName);
            UpdateStatus("AssetBundle map loaded!");

            abRequest.assetBundle.Unload(false);
        }

        isLoading = false;
        if (assetLoadBtn != null) assetLoadBtn.interactable = true;
    }

    private void UpdateStatus(string msg)
    {
        Debug.Log("[MapLoader] " + msg);
        if (statusText != null)
            statusText.text = msg;
    }

    private void OnDestroy()
    {
        if (currentMap != null)
            Destroy(currentMap);
    }
}