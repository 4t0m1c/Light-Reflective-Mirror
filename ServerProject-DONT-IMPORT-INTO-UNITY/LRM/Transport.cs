﻿using System;

namespace Mirror {
    /// <summary>
    /// Abstract transport layer component
    /// </summary>
    /// <remarks>
    /// <h2>
    ///   Transport Rules 
    /// </h2>
    /// <list type="bullet">
    ///   <listheader><description>
    ///     All transports should follow these rules so that they work correctly with mirror
    ///   </description></listheader>
    ///   <item><description>
    ///     When Monobehaviour is disabled the Transport should not invoke callbacks
    ///   </description></item>
    ///   <item><description>
    ///     Callbacks should be invoked on main thread. It is best to do this from LateUpdate
    ///   </description></item>
    ///   <item><description>
    ///     Callbacks can be invoked after <see cref="ServerStop"/> or <see cref="ClientDisconnect"/> as been called
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ServerStop"/> or <see cref="ClientDisconnect"/> can be called by mirror multiple times
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="Available"/> should check the platform and 32 vs 64 bit if the transport only works on some of them
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="GetMaxPacketSize"/> should return size even if transport is not running
    ///   </description></item>
    ///   <item><description>
    ///     Default channel should be reliable <see cref="Channels.DefaultReliable"/>
    ///   </description></item>
    /// </list>
    /// </remarks>
    public abstract class Transport {
        /// <summary>
        /// The current transport used by Mirror.
        /// </summary>
        public static Transport activeTransport;

        /// <summary>
        /// Is this transport available in the current platform?
        /// <para>Some transports might only be available in mobile</para>
        /// <para>Many will not work in webgl</para>
        /// <para>Example usage: return Application.platform == RuntimePlatform.WebGLPlayer</para>
        /// </summary>
        /// <returns>True if this transport works in the current platform</returns>
        public abstract bool Available ();

        #region Client

        /// <summary>
        /// Notify subscribers when when this client establish a successful connection to the server
        /// <para>callback()</para>
        /// </summary>
        public Action OnClientConnected = () => Console.WriteLine ("OnClientConnected called with no handler");

        /// <summary>
        /// Notify subscribers when this client receive data from the server
        /// <para>callback(ArraySegment&lt;byte&gt; data, int channel)</para>
        /// </summary>
        public Action<ArraySegment<byte>, int> OnClientDataReceived = (data, channel) => Console.WriteLine ("OnClientDataReceived called with no handler");

        /// <summary>
        /// Notify subscribers when this client encounters an error communicating with the server
        /// <para>callback(Exception e)</para>
        /// </summary>
        public Action<Exception> OnClientError = (error) => Console.WriteLine ("OnClientError called with no handler");

        /// <summary>
        /// Notify subscribers when this client disconnects from the server
        /// <para>callback()</para>
        /// </summary>
        public Action OnClientDisconnected = () => Console.WriteLine ("OnClientDisconnected called with no handler");

        /// <summary>
        /// Determines if we are currently connected to the server
        /// </summary>
        /// <returns>True if a connection has been established to the server</returns>
        public abstract bool ClientConnected ();

        /// <summary>
        /// Establish a connection to a server
        /// </summary>
        /// <param name="address">The IP address or FQDN of the server we are trying to connect to</param>
        public abstract void ClientConnect (string address);

        /// <summary>
        /// Establish a connection to a server
        /// </summary>
        /// <param name="uri">The address of the server we are trying to connect to</param>
        public virtual void ClientConnect (Uri uri) {
            // By default, to keep backwards compatibility, just connect to the host
            // in the uri
            ClientConnect (uri.Host);
        }

        /// <summary>
        /// Send data to the server
        /// </summary>
        /// <param name="channelId">The channel to use.  0 is the default channel,
        /// but some transports might want to provide unreliable, encrypted, compressed, or any other feature
        /// as new channels</param>
        /// <param name="segment">The data to send to the server. Will be recycled after returning, so either use it directly or copy it internally. This allows for allocation-free sends!</param>
        public abstract void ClientSend (ArraySegment<byte> segment, int channelId = Channels.Reliable);

        /// <summary>
        /// Disconnect this client from the server
        /// </summary>
        public abstract void ClientDisconnect ();

        #endregion

        #region Server

        /// <summary>
        /// Retrieves the address of this server.
        /// Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public abstract Uri ServerUri ();

        /// <summary>
        /// Notify subscribers when a client connects to this server
        /// <para>callback(int connId)</para>
        /// </summary>
        public Action<int> OnServerConnected = (connId) => Console.WriteLine ("OnServerConnected called with no handler");

        /// <summary>
        /// Notify subscribers when this server receives data from the client
        /// <para>callback(int connId, ArraySegment&lt;byte&gt; data, int channel)</para>
        /// </summary>
        public Action<int, ArraySegment<byte>, int> OnServerDataReceived = (connId, data, channel) => Console.WriteLine ("OnServerDataReceived called with no handler");

        /// <summary>
        /// Notify subscribers when this server has some problem communicating with the client
        /// <para>callback(int connId, Exception e)</para>
        /// </summary>
        public Action<int, Exception> OnServerError = (connId, error) => Console.WriteLine ("OnServerError called with no handler");

        /// <summary>
        /// Notify subscribers when a client disconnects from this server
        /// <para>callback(int connId)</para>
        /// </summary>
        public Action<int> OnServerDisconnected = (connId) => Console.WriteLine ("OnServerDisconnected called with no handler");

        /// <summary>
        /// Determines if the server is up and running
        /// </summary>
        /// <returns>true if the transport is ready for connections from clients</returns>
        public abstract bool ServerActive ();

        /// <summary>
        /// Start listening for clients
        /// </summary>
        public abstract void ServerStart (ushort port);

        /// <summary>
        /// Send data to a client.
        /// </summary>
        /// <param name="connectionId">The client connection id to send the data to</param>
        /// <param name="channelId">The channel to be used.  Transports can use channels to implement
        /// other features such as unreliable, encryption, compression, etc...</param>
        /// <param name="data"></param>
        public abstract void ServerSend (int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable);

        /// <summary>
        /// Disconnect a client from this server.  Useful to kick people out.
        /// </summary>
        /// <param name="connectionId">the id of the client to disconnect</param>
        /// <returns>true if the client was kicked</returns>
        public abstract bool ServerDisconnect (int connectionId);

        /// <summary>
        /// Get the client address
        /// </summary>
        /// <param name="connectionId">id of the client</param>
        /// <returns>address of the client</returns>
        public abstract string ServerGetClientAddress (int connectionId);

        /// <summary>
        /// Stop listening for clients and disconnect all existing clients
        /// </summary>
        public abstract void ServerStop ();

        #endregion

        /// <summary>
        /// The maximum packet size for a given channel.  Unreliable transports
        /// usually can only deliver small packets. Reliable fragmented channels
        /// can usually deliver large ones.
        ///
        /// GetMaxPacketSize needs to return a value at all times. Even if the
        /// Transport isn't running, or isn't Available(). This is because
        /// Fallback and Multiplex transports need to find the smallest possible
        /// packet size at runtime.
        /// </summary>
        /// <param name="channelId">channel id</param>
        /// <returns>the size in bytes that can be sent via the provided channel</returns>
        public abstract int GetMaxPacketSize (int channelId = Channels.Reliable);

        public virtual int GetBatchThreshold (int channelId = Channels.Reliable) {
            return GetMaxPacketSize (channelId);
        }

        /// <summary>
        /// Shut down the transport, both as client and server
        /// </summary>
        public abstract void Shutdown ();

        // block Update() to force Transports to use LateUpdate to avoid race
        // conditions. messages should be processed after all the game state
        // was processed in Update.
        // -> in other words: use LateUpdate!
        // -> uMMORPG 480 CCU stress test: when bot machine stops, it causes
        //    'Observer not ready for ...' log messages when using Update
        // -> occupying a public Update() function will cause Warnings if a
        //    transport uses Update.
        //
        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
#pragma warning disable UNT0001 // Empty Unity message
        public abstract void Update ();

        public abstract void Awake ();
#pragma warning restore UNT0001 // Empty Unity message

        /// <summary>
        /// called when quitting the application by closing the window / pressing stop in the editor
        /// <para>virtual so that inheriting classes' OnApplicationQuit() can call base.OnApplicationQuit() too</para>
        /// </summary>
        public virtual void OnApplicationQuit () {
            // stop transport (e.g. to shut down threads)
            // (when pressing Stop in the Editor, Unity keeps threads alive
            //  until we press Start again. so if Transports use threads, we
            //  really want them to end now and not after next start)
            Shutdown ();
        }
    }
}