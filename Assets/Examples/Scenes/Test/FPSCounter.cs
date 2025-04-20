using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private int curFPS;
    [SerializeField] TextMeshProUGUI display_Text;

    private const int frameRange = 60;
    private int[] fpsBuffer = new int[frameRange];
    private int fpsBufferIndex;

    public void Update()
    {
        curFPS = GetFPS();
        UpdateBuffer(curFPS);
        int averge = GetAverge();
        display_Text.text = curFPS.ToString() + " FPS\n" + averge.ToString() + " Averge";
    }

    private void UpdateBuffer(int fps)
    {
        fpsBuffer[fpsBufferIndex++] = fps;
        if (fpsBufferIndex >= frameRange)
            fpsBufferIndex = 0;
    }

    private int GetFPS()
    {
        return (int)(1f / Time.unscaledDeltaTime);
    }
    private int GetAverge()
    {
        int sum = 0;
        int count = 0;
        for (int i = 0; i < frameRange; i++)
        {
            if (fpsBuffer[i] > 0)
            {
                sum += fpsBuffer[i];
                count++;
            }
        }

        int averageFPS = (int)(sum / frameRange);
        return averageFPS;
    }
}
