using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using ComputeryLib.Commands;
using ComputeryLib.Utilities;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.CLI;

public class PipeHandler : MonoBehaviour {
    public static PipeHandler? Instance { get; private set; }
    
    public static void CreatePipe(string pipeName) {
        if (Instance != null) { Destroy(Instance.gameObject); }
        
        GameObject pipeHandlerObject = new GameObject("PipeHandler");
        DontDestroyOnLoad(pipeHandlerObject);
        Instance = pipeHandlerObject.AddComponent<PipeHandler>();
        Instance.InitializePipe(pipeName);
    }
    
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _pipeWriter;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private float _nextPlayerListUpdate;
    
    public void InitializePipe(string pipeName) {
        _cts = new CancellationTokenSource();
        _ = PipeClientLoop(pipeName, _cts.Token);
    }
    
    public new void SendMessage(string message) {
        if (_pipeWriter == null || _pipeClient?.IsConnected != true) { return; }
        try { _pipeWriter.WriteLine(message); }
        catch {
            /* Ignored */
        }
    }
    
    private async Task PipeClientLoop(string pipeName, CancellationToken cancellationToken) {
        StreamReader? reader = null;
        try {
            _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipeClient.ConnectAsync(cancellationToken);
            
            _pipeWriter = new StreamWriter(_pipeClient) { AutoFlush = true };
            reader = new StreamReader(_pipeClient);
            
            while (!cancellationToken.IsCancellationRequested && _pipeClient.IsConnected) {
                string? line = await reader.ReadLineAsync();
                if (line != null) { _messageQueue.Enqueue(line); }
            }
        }
        catch (Exception e) {
            Plugin.Logger.LogError(e);
        }
        finally {
            reader?.Dispose();
            _pipeWriter?.Dispose();
            _pipeWriter = null;
            _pipeClient?.Dispose();
            _pipeClient = null;
        }
    }
    
    public void Update() {
        while (_messageQueue.TryDequeue(out string? message)) { ChatCommandManager.HandleConsoleMessage(message); }
        
        if (Time.time >= _nextPlayerListUpdate && WorldUtility.TryGetWorld(out ServerClient? world) && world!.GameRoomReference != null) {
            _nextPlayerListUpdate = Time.time + 15f;
            List<TABGPlayerServer> players = world.GameRoomReference.Players;
            string[] epicUsernames = new string[players.Count];
            for (int index = 0; index < players.Count; index++) {
                TABGPlayerServer player = players[index];
                epicUsernames[index] = player.EpicUserName;
            }
            string json = JsonUtility.ToJson(new { type = "playerList", players = epicUsernames });
            SendMessage(json);
        }
    }
    
    private void OnDestroy() {
        _cts?.Cancel();
        _pipeClient?.Dispose();
        _cts?.Dispose();
    }
}