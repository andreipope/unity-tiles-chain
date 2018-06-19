using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Loom.Unity3d.Samples.TilesChain
{
    /// <summary>
    /// Abstracts interaction with the contract.
    /// </summary>
    public class TileChainContractClient
    {
        private readonly byte[] privateKey;
        private readonly byte[] publicKey;
        private readonly ILogger logger;
        private readonly Queue<Action> eventActions = new Queue<Action>();
        private Contract contract;
        private DAppChainClient client;
        private IRPCClient writer;
        private IRPCClient reader;

        public event Action<JsonTileMapState> TileMapStateUpdated;

        public TileChainContractClient(byte[] privateKey, byte[] publicKey, ILogger logger)
        {
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.logger = logger;
        }

        public void Update()
        {
            while (this.eventActions.Count > 0)
            {
                Action action = this.eventActions.Dequeue();
                action();
            }
        }

        public bool IsConnected => this.reader.IsConnected;

        public async Task ConnectToContract()
        {
            if (this.contract == null)
            {
                this.contract = await GetContract();
            }
        }

        private async Task<Contract> GetContract()
        {
            this.writer = RPCClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:46657/websocket")
                .Create();

            this.reader = RPCClientFactory.Configure()
                .WithLogger(Debug.unityLogger)
                .WithWebSocket("ws://127.0.0.1:9999/queryws")
                .Create();

            this.client = new DAppChainClient(this.writer, this.reader)
                { Logger = this.logger };

            // required middleware
            this.client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
            {
                new NonceTxMiddleware
                {
                    PublicKey = this.publicKey,
                    Client = this.client
                },
                new SignedTxMiddleware(this.privateKey)
            });

            var contractAddr = await this.client.ResolveContractAddressAsync("TileChain");
            var callerAddr = Address.FromPublicKey(this.publicKey);
            Contract contract = new Contract(this.client, contractAddr, callerAddr);
            contract.EventReceived += ChainEventReceivedHandler;
            return contract;
        }

        private void ChainEventReceivedHandler(object sender, ChainEventArgs e)
        {
            if (e.EventName != "onTileMapStateUpdate")
                return;

            string stringData = Encoding.UTF8.GetString(e.Data);
            JsonTileMapState tileMapStateParsed = JsonUtility.FromJson<JsonTileMapState>(stringData);
            this.eventActions.Enqueue(() => {
                TileMapStateUpdated?.Invoke(tileMapStateParsed);
            });
        }

        public async Task<JsonTileMapState> GetTileMapState()
        {
            await ConnectToContract();

            TileMapState result = await this.contract.StaticCallAsync<TileMapState>("GetTileMapState", new TileMapState());
            if (result == null)
                throw new Exception("Smart contract didn't return anything!");

            JsonTileMapState jsonTileMapState = JsonUtility.FromJson<JsonTileMapState>(result.Data);
            return jsonTileMapState;
        }

        public async Task SetTileMapState(JsonTileMapState jsonTileMapState)
        {
            await ConnectToContract();

            TileMapState tileMapStateTx = new TileMapState();
            tileMapStateTx.Data = JsonUtility.ToJson(jsonTileMapState);
            await this.contract.CallAsync("SetTileMapState", tileMapStateTx);
        }
    }
}
