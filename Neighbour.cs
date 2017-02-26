using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    class Neighbour
    {
        public MapObjectData Vertex;
        public Direction OffsetDirection;

        public Neighbour(MapObjectData vertex, Direction offsetDirection)
        {
            Vertex = vertex;
            OffsetDirection = offsetDirection;
        }
    }
}
