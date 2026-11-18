using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;

public sealed class VCSStudioPreflight : MonoBehaviour
{
    [Serializable] private class ProbePayload
    {
        public string type;
        public PoseLandmark[] landmarks;
    }

    public bool Connected { get; private set; }
    public float LastPoseTime { get; private set; } = -10000f;
    public string LastError { get; private set; }
    public bool HasFreshPose(float maxAge)
    {
        return Connected && Time.realtimeSinceStartup - LastPoseTime < Mathf.Max(0.05f, maxAge);
    }

    private WebSocket socket;
    private bool active;
    private bool connecting;
    private float nextRetry;
    private string endpoint;

    public void Begin(string url)
    {
        End();
        endpoint = url;
        active = true;
        LastError = null;
        LastPoseTime = -10000f;
        nextRetry = 0f;
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif
        if (active && socket == null && !connecting && Time.unscaledTime >= nextRetry)
            ConnectOnce();
    }

    private async void ConnectOnce()
    {
        if (!active || connecting || socket != null) return;
        connecting = true;
        WebSocket candidate;
        try { candidate = new WebSocket(endpoint); }
        catch (Exception e)
        {
            LastError = e.Message;
            connecting = false;
            nextRetry = Time.unscaledTime + 1.2f;
            return;
        }
        socket = candidate;
        candidate.OnOpen += () =>
        {
            if (!active || socket != candidate) { CloseQuietly(candidate); return; }
            Connected = true;
            LastError = null;
        };
        candidate.OnError += error =>
        {
            if (socket != candidate) return;
            LastError = error;
            Connected = false;
            socket = null;
            nextRetry = Time.unscaledTime + 1.2f;
            CloseQuietly(candidate);
        };
        candidate.OnClose += code =>
        {
            if (socket != candidate) return;
            Connected = false;
            socket = null;
            nextRetry = Time.unscaledTime + 1.2f;
        };
        candidate.OnMessage += bytes =>
        {
            if (!active || socket != candidate) return;
            try
            {
                ProbePayload p = JsonUtility.FromJson<ProbePayload>(Encoding.UTF8.GetString(bytes));
                if (p != null && p.type == "pose" && p.landmarks != null && p.landmarks.Length == 33)
                    LastPoseTime = Time.realtimeSinceStartup;
            }
            catch (Exception) { /* Ignore an incomplete/non-pose message. */ }
        };
        try
        {
            await candidate.Connect();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Connected = false;
        }
        finally
        {
            connecting = false;
            if (!Connected && socket == candidate)
            {
                socket = null;
                try { await candidate.Close(); } catch (Exception) { }
                nextRetry = Time.unscaledTime + 1.2f;
            }
        }
    }

    public void End()
    {
        active = false;
        Connected = false;
        LastPoseTime = -10000f;
        WebSocket old = socket;
        socket = null;
        if (old != null) CloseQuietly(old);
    }

    private async void CloseQuietly(WebSocket ws)
    {
        try { await ws.Close(); } catch (Exception) { }
    }

    private void OnDestroy() { End(); }
}
