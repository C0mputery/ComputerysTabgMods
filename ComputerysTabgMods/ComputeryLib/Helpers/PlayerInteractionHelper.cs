using System;
using System.Collections.Generic;
using System.Text;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class PlayerInteractionHelper {
    public static void PrivateMessageOrServerLog(string message, TABGPlayerServer? sender) {
        if (sender == null) { Plugin.Logger.LogInfo(message); }
        else { PrivateMessage(message, sender); }
    }
    
    public static void PrivateMessage(string message, TABGPlayerServer sender) {
        string[] chunks = SplitMessageIntoChunks(message, 200);
        
        Quaternion rotation = Quaternion.Euler(0, sender.PlayerRotation.y, 0);
        Vector3 forwardDirection = rotation * Vector3.forward;
        Vector3 rightDirection = rotation * Vector3.right;
        
        Vector3 center = sender.PlayerPosition + forwardDirection * 3f;
        for (int i = 0; i < chunks.Length; i++) {
            string chunk = chunks[i];
            Vector3 currentChatThrowPosition = center + rightDirection * ((i - (chunks.Length - 1) / 2f) * 2f);
            ThrowChunk(chunk, currentChatThrowPosition, sender);
        }
    }
    
    private static string[] SplitMessageIntoChunks(string message, int maxChunkByteSize) {
        if (string.IsNullOrEmpty(message)) { return [""]; }
        
        List<string> chunks = [];
        int currentIndex = 0;
        
        while (currentIndex < message.Length) {
            int chunkLength = 0;
            
            while (currentIndex + chunkLength < message.Length) {
                string potentialChunk = message.Substring(currentIndex, chunkLength + 1);
                int potentialBytes = Encoding.Unicode.GetByteCount(potentialChunk);
                
                if (potentialBytes <= maxChunkByteSize) { chunkLength++; }
                else { break; }
            }
            
            if (currentIndex + chunkLength >= message.Length) {
                chunks.Add(message.Substring(currentIndex));
                break;
            }
            
            int lastSpaceIndex = message.LastIndexOf(' ', currentIndex + chunkLength - 1);
            
            if (lastSpaceIndex > currentIndex && lastSpaceIndex < currentIndex + chunkLength) {
                chunks.Add(message.Substring(currentIndex, lastSpaceIndex - currentIndex));
                currentIndex = lastSpaceIndex + 1;
            }
            else {
                if (chunkLength == 0) { chunkLength = 1; }
                chunks.Add(message.Substring(currentIndex, chunkLength));
                currentIndex += chunkLength;
            }
        }
        
        return chunks.ToArray();
    }
    
    private static void ThrowChunk(string chunk, Vector3 position, TABGPlayerServer sender) {
        if (WorldHelper.TryGetWorld(out ServerClient? world)) {
            world.SendMessageToClients(EventCode.ThrowChatMessage, PacketHelper.BuildThrowChatMessage(sender.PlayerIndex, position, chunk), sender.PlayerIndex, true);
        }
    }
}