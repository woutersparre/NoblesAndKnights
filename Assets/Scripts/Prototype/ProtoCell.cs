using System.Collections.Generic;
using UnityEngine;

namespace SphereVoronoiPrototype
{
    [System.Serializable]
    public class ProtoCell
    {
        public int Id;
        public Vector3 Seed;           // Point on unit sphere
        public Vector3[] Vertices;     // Voronoi vertices on sphere (ordered around seed)
        public int[] Neighbors;        // Neighbor cell ids
        public Color Color;
        public bool IsPlayable;
    }
}
