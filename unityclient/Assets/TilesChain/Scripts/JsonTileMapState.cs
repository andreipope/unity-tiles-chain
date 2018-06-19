using System;
using System.Collections.Generic;
using UnityEngine;

namespace Loom.Unity3d.Samples.TilesChain {
    [Serializable]
    public class JsonTileMapState {
        public List<Tile> tiles = new List<Tile>();

        [Serializable]
        public struct Tile {
            public Vector2Int point;
            public Color color;

            [Serializable]
            public struct Color {
                public int r, g, b;
            }
        }
    }
}