/// @file
/// @brief File implementing helper functions for setup and process management.
using UnityEditor;
using System.IO;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine.Networking;

/// @defgroup llm LLM
/// @defgroup template Chat Templates
/// @defgroup utils Utils
namespace LLMUnity
{
    /// \cond HIDE
    public sealed class FloatAttribute : PropertyAttribute
    {
        public float Min { get; private set; }
        public float Max { get; private set; }

        public FloatAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
    public sealed class IntAttribute : PropertyAttribute
    {
        public int Min { get; private set; }
        public int Max { get; private set; }

        public IntAttribute(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    public class LLMAttribute : PropertyAttribute {}
    public class LLMAdvancedAttribute : PropertyAttribute {}
    public class LocalRemoteAttribute : PropertyAttribute {}
    public class RemoteAttribute : PropertyAttribute {}
    public class LocalAttribute : PropertyAttribute {}
    public class ModelAttribute : PropertyAttribute {}
    public class ModelDownloadAttribute : ModelAttribute {}
    public class ModelDownloadAdvancedAttribute : ModelAdvancedAttribute {}
    public class ModelAdvancedAttribute : PropertyAttribute {}
    public class ChatAttribute : PropertyAttribute {}
    public class ChatAdvancedAttribute : PropertyAttribute {}
    public class LLMUnityAttribute : PropertyAttribute {}

    public class NotImplementedException : Exception
    {
        public NotImplementedException() : base("The method needs to be implemented by subclasses.") {}
    }

    public delegate void EmptyCallback();
    public delegate void Callback<T>(T message);
    public delegate Task TaskCallback<T>(T message);
    public delegate T2 ContentCallback<T, T2>(T message);
    public delegate void ActionCallback(string source, string target);

    [Serializable]
    public struct StringPair
    {
        public string source;
        public string target;
    }

    [Serializable]
    public class ListStringPair
    {
        public List<StringPair> pairs;
    }
    /// \endcond

    /// @ingroup utils
    /// <summary>
    /// Class implementing helper functions for setup and process management.
    /// </summary>
    public class LLMUnitySetup
    {
        // DON'T CHANGE! the version is autocompleted with a GitHub action
        /// <summary> LLM for Unity version </summary>
        public static string Version = "v2.1.2";
        /// <summary> LlamaLib version </summary>
        public static string LlamaLibVersion = "v1.1.8";
        /// <summary> LlamaLib url </summary>
        public static string LlamaLibURL = $"https://github.com/undreamai/LlamaLib/releases/download/{LlamaLibVersion}/undreamai-{LlamaLibVersion}-llamacpp.zip";
        /// <summary> LlamaLib path </summary>
        public static string libraryPath = GetAssetPath(Path.GetFileName(LlamaLibURL).Replace(".zip", ""));
        /// <summary> LLMnity store path </summary>
        public static string LLMUnityStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLMUnity");
        /// <summary> Model download path </summary>
        public static string modelDownloadPath = Path.Combine(LLMUnityStore, "models");
        /// <summary> Path of file with build information for runtime </summary>
        public static string LLMManagerPath = GetAssetPath("LLMManager.json");

        /// <summary> Default models for download </summary>
        [HideInInspector] public static readonly (string, string, string)[] modelOptions = new(string, string, string)[]
        {
            ("Llama 3 7B (medium, best overall)", "https://huggingface.co/lmstudio-community/Meta-Llama-3-8B-Instruct-GGUF/resolve/main/Meta-Llama-3-8B-Instruct-Q4_K_M.gguf?download=true", "https://huggingface.co/meta-llama/Meta-Llama-3-8B/blob/main/LICENSE"),
            ("Mistral 7B Instruct v0.2 (medium, great overall)", "https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/mistral-7b-instruct-v0.2.Q4_K_M.gguf?download=true", null),
            ("OpenHermes 2.5 7B (medium, good for conversation)", "https://huggingface.co/TheBloke/OpenHermes-2.5-Mistral-7B-GGUF/resolve/main/openhermes-2.5-mistral-7b.Q4_K_M.gguf?download=true", null),
            ("Phi 3 (small, great small model)", "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf?download=true", null),
            ("Qwen 2 0.5B (tiny, useful for mobile)", "https://huggingface.co/Qwen/Qwen2-0.5B-Instruct-GGUF/resolve/main/qwen2-0_5b-instruct-q4_k_m.gguf?download=true", null),
        };

        /// <summary> Add callback function to call for error logs </summary>
        public static void AddErrorCallBack(Callback<string> callback)
        {
            errorCallbacks.Add(callback);
        }

        /// <summary> Remove callback function added for error logs </summary>
        public static void RemoveErrorCallBack(Callback<string> callback)
        {
            errorCallbacks.Remove(callback);
        }

        /// <summary> Remove all callback function added for error logs </summary>
        public static void ClearErrorCallBacks()
        {
            errorCallbacks.Clear();
        }

        /// \cond HIDE
        public enum DebugModeType
        {
            All,
            Warning,
            Error,
            None
        }
        [LLMUnity] public static DebugModeType DebugMode = DebugModeType.All;
        static List<Callback<string>> errorCallbacks = new List<Callback<string>>();
        static readonly object lockObject = new object();
        static Dictionary<string, Task> androidExtractTasks = new Dictionary<string, Task>();

        public static void Log(string message)
        {
            if ((int)DebugMode > (int)DebugModeType.All) return;
            Debug.Log(message);
        }

        public static void LogWarning(string message)
        {
            if ((int)DebugMode > (int)DebugModeType.Warning) return;
            Debug.LogWarning(message);
        }

        public static void LogError(string message)
        {
            if ((int)DebugMode > (int)DebugModeType.Error) return;
            Debug.LogError(message);
            foreach (Callback<string> errorCallback in errorCallbacks) errorCallback(message);
        }

        static string DebugModeKey = "DebugMode";
        static void LoadDebugMode()
        {
            DebugMode = (DebugModeType)PlayerPrefs.GetInt(DebugModeKey, (int)DebugModeType.All);
        }

        public static void SetDebugMode(DebugModeType newDebugMode)
        {
            if (DebugMode == newDebugMode) return;
            DebugMode = newDebugMode;
            PlayerPrefs.SetInt(DebugModeKey, (int)DebugMode);
            PlayerPrefs.Save();
        }

        public static string GetAssetPath(string relPath = "")
        {
            // Path to store llm server binaries and models
            string assetsDir = Application.platform == RuntimePlatform.Android ? Application.persistentDataPath : Application.streamingAssetsPath;
            return Path.Combine(assetsDir, relPath).Replace('\\', '/');
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static async Task InitializeOnLoad()
        {
            await DownloadLibrary();
            LoadDebugMode();
        }

#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        void InitializeOnLoad()
        {
            LoadDebugMode();
        }

#endif

        static Dictionary<string, ResumingWebClient> downloadClients = new Dictionary<string, ResumingWebClient>();

        public static void CancelDownload(string savePath)
        {
            if (!downloadClients.ContainsKey(savePath)) return;
            downloadClients[savePath].CancelDownloadAsync();
            downloadClients.Remove(savePath);
        }

        public static async Task DownloadFile(
            string fileUrl, string savePath, bool overwrite = false,
            Callback<string> callback = null, Callback<float> progressCallback = null
        )
        {
            if (File.Exists(savePath) && !overwrite)
            {
                Log($"File already exists at: {savePath}");
            }
            else
            {
                Log($"Downloading {fileUrl} to {savePath}...");
                string tmpPath = Path.Combine(Application.temporaryCachePath, Path.GetFileName(savePath));

                ResumingWebClient client = new ResumingWebClient();
                downloadClients[savePath] = client;
                await client.DownloadFileTaskAsyncResume(new Uri(fileUrl), tmpPath, !overwrite, progressCallback);
                downloadClients.Remove(savePath);
#if UNITY_EDITOR
                AssetDatabase.StartAssetEditing();
#endif
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                File.Move(tmpPath, savePath);
#if UNITY_EDITOR
                AssetDatabase.StopAssetEditing();
#endif
                Log($"Download complete!");
            }

            progressCallback?.Invoke(1f);
            callback?.Invoke(savePath);
        }

        public static async Task AndroidExtractFile(string assetName, bool overwrite = false, bool log = true, int chunkSize = 1024*1024)
        {
            Task extractionTask;
            lock (lockObject)
            {
                if (!androidExtractTasks.TryGetValue(assetName, out extractionTask))
                {
                    extractionTask = AndroidExtractFileOnce(assetName, overwrite, log, chunkSize);
                    androidExtractTasks[assetName] = extractionTask;
                }
            }
            await extractionTask;
        }

        public static async Task AndroidExtractFileOnce(string assetName, bool overwrite = false, bool log = true, int chunkSize = 1024*1024)
        {
            string source = "jar:file://" + Application.dataPath + "!/assets/" + assetName;
            string target = GetAssetPath(assetName);
            if (!overwrite && File.Exists(target))
            {
                if (log) Log($"File {target} already exists");
                return;
            }

            Log($"Extracting {source} to {target}");

            // UnityWebRequest to read the file from StreamingAssets
            UnityWebRequest www = UnityWebRequest.Get(source);
            // Send the request and await its completion
            var operation = www.SendWebRequest();

            while (!operation.isDone) await Task.Delay(1);
            if (www.result != UnityWebRequest.Result.Success)
            {
                LogError("Failed to load file from StreamingAssets: " + www.error);
            }
            else
            {
                byte[] buffer = new byte[chunkSize];
                using (Stream responseStream = new MemoryStream(www.downloadHandler.data))
                using (FileStream fileStream = new FileStream(target, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;
                    while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                    }
                }
            }
        }

        public static async Task AndroidExtractAsset(string path, bool overwrite = false)
        {
            if (Application.platform != RuntimePlatform.Android) return;
            await AndroidExtractFile(Path.GetFileName(path), overwrite);
        }

        public static bool IsSubPath(string childPath, string parentPath)
        {
            string fullParentPath = Path.GetFullPath(parentPath).Replace('\\', '/');
            string fullChildPath = Path.GetFullPath(childPath).Replace('\\', '/');
            return fullChildPath.StartsWith(fullParentPath, StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_EDITOR

        [HideInInspector] public static float libraryProgress = 1;

        private static async Task DownloadLibrary()
        {
            if (libraryProgress < 1) return;
            libraryProgress = 0;
            string libZip = Path.Combine(Application.temporaryCachePath, Path.GetFileName(LlamaLibURL));
            if (!Directory.Exists(libraryPath))
            {
                await DownloadFile(LlamaLibURL, libZip, true, null, SetLibraryProgress);
                AssetDatabase.StartAssetEditing();
                ZipFile.ExtractToDirectory(libZip, libraryPath);
                string androidDir = Path.Combine(libraryPath, "android");
                if (Directory.Exists(androidDir))
                {
                    string androidPluginDir = Path.Combine(Application.dataPath, "Plugins", "Android");
                    Directory.CreateDirectory(androidPluginDir);
                    Directory.Move(androidDir, Path.Combine(androidPluginDir, Path.GetFileName(libraryPath)));
                }
                foreach (string librarySubPath in Directory.GetDirectories(libraryPath))
                {
                    if (Path.GetFileName(librarySubPath).StartsWith("android"))
                    {
                        string pluginPath = Path.Combine(Application.dataPath, "Plugins", "Android", Path.GetFileName(librarySubPath));
                        Directory.Move(librarySubPath, pluginPath);
                    }
                }
                AssetDatabase.StopAssetEditing();
                File.Delete(libZip);
            }
            libraryProgress = 1;
        }

        private static void SetLibraryProgress(float progress)
        {
            libraryProgress = progress;
        }

        public static string AddAsset(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                LogError($"{assetPath} does not exist!");
                return null;
            }
            string filename = Path.GetFileName(assetPath);
            string fullPath = GetAssetPath(filename);
            AssetDatabase.StartAssetEditing();
            foreach (string path in new string[] {fullPath, fullPath + ".meta"})
            {
                if (File.Exists(path)) File.Delete(path);
            }
            File.Copy(assetPath, fullPath);
            AssetDatabase.StopAssetEditing();
            return filename;
        }

#endif
        /// \endcond
        public static int GetMaxFreqKHz(int cpuId)
        {
            string[] paths = new string[]
            {
                $"/sys/devices/system/cpu/cpufreq/stats/cpu{cpuId}/time_in_state",
                $"/sys/devices/system/cpu/cpu{cpuId}/cpufreq/stats/time_in_state",
                $"/sys/devices/system/cpu/cpu{cpuId}/cpufreq/cpuinfo_max_freq"
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;

                int maxFreqKHz = 0;
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] parts = line.Split(' ');
                        if (parts.Length > 0 && int.TryParse(parts[0], out int freqKHz))
                        {
                            if (freqKHz > maxFreqKHz)
                            {
                                maxFreqKHz = freqKHz;
                            }
                        }
                    }
                }
                if (maxFreqKHz != 0) return maxFreqKHz;
            }
            return -1;
        }

        public static bool IsSmtCpu(int cpuId)
        {
            string[] paths = new string[]
            {
                $"/sys/devices/system/cpu/cpu{cpuId}/topology/core_cpus_list",
                $"/sys/devices/system/cpu/cpu{cpuId}/topology/thread_siblings_list"
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains(",") || line.Contains("-"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates the number of big cores in Android similarly to ncnn (https://github.com/Tencent/ncnn)
        /// </summary>
        /// <returns></returns>
        public static int AndroidGetNumBigCores()
        {
            int maxFreqKHzMin = int.MaxValue;
            int maxFreqKHzMax = 0;
            List<int> cpuMaxFreqKHz = new List<int>();
            List<bool> cpuIsSmtCpu = new List<bool>();

            try
            {
                string cpuPath = "/sys/devices/system/cpu/";
                int coreIndex;
                if (Directory.Exists(cpuPath))
                {
                    foreach (string cpuDir in Directory.GetDirectories(cpuPath))
                    {
                        string dirName = Path.GetFileName(cpuDir);
                        if (!dirName.StartsWith("cpu")) continue;
                        if (!int.TryParse(dirName.Substring(3), out coreIndex)) continue;

                        int maxFreqKHz = GetMaxFreqKHz(coreIndex);
                        cpuMaxFreqKHz.Add(maxFreqKHz);
                        if (maxFreqKHz > maxFreqKHzMax) maxFreqKHzMax = maxFreqKHz;
                        if (maxFreqKHz < maxFreqKHzMin)  maxFreqKHzMin = maxFreqKHz;
                        cpuIsSmtCpu.Add(IsSmtCpu(coreIndex));
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }

            int numBigCores = 0;
            int numCores = SystemInfo.processorCount;
            int maxFreqKHzMedium = (maxFreqKHzMin + maxFreqKHzMax) / 2;
            if (maxFreqKHzMedium == maxFreqKHzMax) numBigCores = numCores;
            else
            {
                for (int i = 0; i < cpuMaxFreqKHz.Count; i++)
                {
                    if (cpuIsSmtCpu[i] || cpuMaxFreqKHz[i] >= maxFreqKHzMedium) numBigCores++;
                }
            }

            if (numBigCores == 0) numBigCores = SystemInfo.processorCount / 2;
            else numBigCores = Math.Min(numBigCores, SystemInfo.processorCount);

            return numBigCores;
        }

        /// <summary>
        /// Calculates the number of big cores in Android similarly to Unity (https://docs.unity3d.com/2022.3/Documentation/Manual/android-thread-configuration.html)
        /// </summary>
        /// <returns></returns>
        public static int AndroidGetNumBigCoresCapacity()
        {
            List<int> capacities = new List<int>();
            int minCapacity = int.MaxValue;
            try
            {
                string cpuPath = "/sys/devices/system/cpu/";
                int coreIndex;
                if (Directory.Exists(cpuPath))
                {
                    foreach (string cpuDir in Directory.GetDirectories(cpuPath))
                    {
                        string dirName = Path.GetFileName(cpuDir);
                        if (!dirName.StartsWith("cpu")) continue;
                        if (!int.TryParse(dirName.Substring(3), out coreIndex)) continue;

                        string capacityPath = Path.Combine(cpuDir, "cpu_capacity");
                        if (!File.Exists(capacityPath)) break;

                        int capacity = int.Parse(File.ReadAllText(capacityPath).Trim());
                        capacities.Add(capacity);
                        if (minCapacity > capacity) minCapacity = capacity;
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }

            int numBigCores = 0;
            foreach (int capacity in capacities)
            {
                if (capacity >= 2 * minCapacity) numBigCores++;
            }

            if (numBigCores == 0 || numBigCores > SystemInfo.processorCount) numBigCores = SystemInfo.processorCount;
            return numBigCores;
        }
    }
}
