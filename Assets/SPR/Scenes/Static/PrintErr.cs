using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PrintErr : MonoBehaviour
{

    [TextArea]public string jsonToAnalize;

    [Serializable]
    public class MethodErrors
    {
        public float ign;
        public float white;
        public float blue;
        public float bayer;
        public float R2;
        public float Plus;
    }

    [Serializable]
    public class Wrapper<T>
    {
        public T data;
    }

    void Start()
    {
        string jsonString = jsonToAnalize;
        var errorsAnalysis = AnalyzeErrors(jsonString);

        foreach (var method in errorsAnalysis)
        {
            Debug.Log($"{method.Key}: Avg={method.Value.AverageError}, Min={method.Value.MinError}, Max={method.Value.MaxError}");
        }
    }

    public Dictionary<string, (float AverageError, float MinError, float MaxError)> AnalyzeErrors(string jsonString)
    {
        // Unity's JsonUtility does not support dictionaries, so manually parse it
        Dictionary<string, MethodErrors> jsonData = JsonUtility.FromJson<Wrapper<Dictionary<string, MethodErrors>>>("{\"data\":" + jsonString + "}").data;
        if (jsonData == null)
        {
            Debug.LogError("Failed to parse JSON");
            return new Dictionary<string, (float, float, float)>();
        }

        var errorData = new Dictionary<string, List<float>>();

        foreach (var entry in jsonData)
        {
            float targetValue = float.Parse(entry.Key);
            var methods = entry.Value;

            foreach (var field in typeof(MethodErrors).GetFields())
            {
                float methodValue = (float)field.GetValue(methods);
                float error = Math.Abs(methodValue - targetValue);

                if (!errorData.ContainsKey(field.Name))
                {
                    errorData[field.Name] = new List<float>();
                }
                errorData[field.Name].Add(error);
            }
        }

        return errorData.ToDictionary(
            kvp => kvp.Key,
            kvp => (
                kvp.Value.Average(),
                kvp.Value.Min(),
                kvp.Value.Max()
            )
        );
    }

    [ContextMenu("Execute")]
    public void Execute()
    {
        string jsonString = jsonToAnalize;
        var errorsAnalysis = AnalyzeErrors(jsonString);

        foreach (var method in errorsAnalysis)
        {
            Debug.Log($"{method.Key}: Avg={method.Value.AverageError}, Min={method.Value.MinError}, Max={method.Value.MaxError}");
        }
        Debug.Log("errorsAnalysis " + errorsAnalysis.Count);
    }
}
