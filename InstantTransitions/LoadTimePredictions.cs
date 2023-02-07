using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace InstantTransitions;

// Logs loading times for rooms and uses them to predict how long it will take to load them in the future.
public static class LoadTimePredictions
{
    // Certain sources of load times may be more accurate than others. This enum keeps track of what should override what.
    public enum Confidence
    {
        NotConfident,
        SomewhatConfident,
        Confident,
        VeryConfident
    }

    private struct Entry
    {
        public float time;
        public Confidence confidence;

        public Entry(float time, Confidence confidence)
        {
            this.time = time;
            this.confidence = confidence;
        }
    }

    private static readonly string ASSEMBLY_FILE_PATH = Assembly.GetExecutingAssembly().Location;
    private static readonly string FILE_NAME = "past_loads.txt";
    private static readonly string PATH = Path.Combine(Path.GetDirectoryName(ASSEMBLY_FILE_PATH), FILE_NAME);

    private static Dictionary<string, Entry> LoadTimes { get; set; }

    static LoadTimePredictions()
    {
        LoadTimes = new Dictionary<string, Entry>();

        for (int i = 0; i < UnitySceneManager.sceneCount; i++)
        {
            LoadTimes.Add(UnitySceneManager.GetSceneAt(i).name, new Entry(1f, Confidence.NotConfident));
        }
    }

    public static float Predict(string scene) => LoadTimes[scene].time;

    public static Confidence GetConfidence(string scene) => LoadTimes[scene].confidence;

    public static void Update(string scene, float time, Confidence confidence)
    {
        if (!(LoadTimes.TryGetValue(scene, out Entry entry) && confidence < entry.confidence))
        {
            LoadTimes[scene] = new Entry(time, confidence);
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(PATH))
            {
                Dictionary<string, Entry> deserialized = JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(PATH)) ?? new();

                foreach (var pair in deserialized)
                {
                    Update(pair.Key, pair.Value.time, pair.Value.confidence);
                }
            }
        }
        catch (Exception ex)
        {
            InstantTransitionsMod.Instance.LogError("Error loading predictions file:");
            InstantTransitionsMod.Instance.LogError(ex);
        }

        try
        {
            // Imports loading times from the loadTimes.txt file in the game's AppData folder.
            // I don't know where this file comes from or if it exists for other people so this code may or may not be useless.
            string loadingTimesPath = Path.Combine(Application.persistentDataPath.Replace('/', Path.DirectorySeparatorChar), "loadTimes.txt");

            if (File.Exists(loadingTimesPath))
            {
                foreach (string line in File.ReadAllLines(loadingTimesPath))
                {
                    string[] parts = line.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries);

                    if (!LoadTimes.ContainsKey(parts[0]))
                    {
                        LoadTimes.Add(parts[0], new Entry(float.Parse(parts[1].Trim()), Confidence.SomewhatConfident));
                    }
                }
            }
        }
        catch { }
    }

    public static void Save() => File.WriteAllText(PATH, JsonConvert.SerializeObject(LoadTimes));
}
