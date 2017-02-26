using System;
using System.Linq;
using HoMM.Sensors;
using HoMM;
using HoMM.ClientClasses;
using System.Collections.Generic;

namespace Homm.Client
{
    class Program
    {
        // Вставьте сюда свой личный CvarcTag для того, чтобы учавствовать в онлайн соревнованиях.
        public static readonly Guid CvarcTag = Guid.Parse("00000000-0000-0000-0000-000000000000");
        static State state = State.NeedResources;

        private static Dictionary<MapObjectData, HashSet<Neighbour>> BuildAdjacencyListFromMap(MapData map)
        {
            var adjList = new Dictionary<MapObjectData, HashSet<Neighbour>>();
            var directions = new[]{
                Direction.Down, Direction.LeftDown, Direction.LeftUp,
                Direction.Up, Direction.RightUp, Direction.RightDown};
            foreach (var mapObject in map.Objects)
            {
                foreach (var direction in directions)
                {
                    var neighborLocation = mapObject.Location.ToLocation().NeighborAt(direction);
                    var neighbor = GetObjectAt(map, neighborLocation);
                    if (neighbor == null)
                        continue;
                    if (adjList.ContainsKey(mapObject))
                        adjList[mapObject].Add(new Neighbour(neighbor, direction));
                    else
                    {
                        adjList[mapObject] = new HashSet<Neighbour> { new Neighbour(neighbor, direction) };
                    }
                }
            }
            return adjList;
        }


        private static Direction? GetNextDirection(MapObjectData currentObject, MapObjectData desiredObject, Dictionary<MapObjectData, HashSet<Neighbour>> adjacencyList)
        {
            var direction = BFS(currentObject, desiredObject, adjacencyList);
            return direction;
        }

        // ReSharper disable once InconsistentNaming
        private static Direction? BFS(MapObjectData currentObject, MapObjectData desiredObject, Dictionary<MapObjectData, HashSet<Neighbour>> adjacencyList)
        {
            var used = new HashSet<MapObjectData>();
            var path = new Dictionary<MapObjectData, MapObjectData>();
            var queue = new Queue<MapObjectData>();
            queue.Enqueue(currentObject);
            // Traverse graph
            while (queue.Count != 0)
            {
                var next = queue.Dequeue();
                foreach (var adjacentObject in adjacencyList[next])
                {
                    var neighbor = adjacentObject.Vertex;
                    if (used.Contains(neighbor))
                        continue;
                    if (neighbor.Wall != null || neighbor.NeutralArmy != null)
                        continue;
                    queue.Enqueue(neighbor);
                    used.Add(neighbor);
                    path[neighbor] = next;
                }
            }

            if (!path.ContainsKey(desiredObject))
                return null;

            // Restore path
            // Find next object
            var nextObject = desiredObject;
            while (path[nextObject] != currentObject)
                nextObject = path[nextObject];
            // Get direction
            foreach (var neighbor in adjacencyList[currentObject])
            {
                if (neighbor.Vertex.Equals(nextObject))
                    return neighbor.OffsetDirection;
            }
            return Direction.Down;
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
                args = new[] { "127.0.0.1", "18700" };
            var ip = args[0];
            var port = int.Parse(args[1]);

            var client = new HommClient<HommSensorData>();

            client.OnSensorDataReceived += Print;
            client.OnInfo += OnInfo;

            var sensorData = client.Configurate(
                ip, 
                port, 
                CvarcTag, 
                timeLimit : 90,             // Продолжительность матча в секундах (исключая время, которое "думает" ваша программа). 
                
                operationalTimeLimit: 5,    // Суммарное время в секундах, которое разрешается "думать" вашей программе. 
                                            // Вы можете увеличить это время для отладки, чтобы ваш клиент не был отключен, пока вы разглядываете программу в режиме дебаггинга
                
                seed: 0,                    // seed карты. Используйте этот параметр, чтобы получать одну и ту же карту и отлаживаться на ней
                                            // Иногда меняйте этот параметр, потому что ваш код должен хорошо работать на любой карте

                spectacularView: true,       // Вы можете отключить графон, заменив параметр на false

                debugMap: false              // Вы можете использовать отладочную простую карту, чтобы лучше понять, как устроен игоровой мир
                
                );


            while (true)
            {
                var resources = GetResources(sensorData.Map);
                var currentObject = GetObjectAt(sensorData.Map, sensorData.Location.ToLocation());
                var adjacencyList = BuildAdjacencyListFromMap(sensorData.Map);

                var desiredObject = default(MapObjectData);
                switch (state)
                {
                    case State.NeedResources:
                        desiredObject = GetNearestObject(sensorData.Location.ToLocation(), resources);
                        break;
                    case State.NeedArmy:
                        break;
                    case State.NeedGlory:
                        break;
                }

                if (desiredObject == null)
                    continue;

                var direction = GetNextDirection(currentObject, desiredObject, adjacencyList);
                if (direction.HasValue)
                    sensorData = client.Move(direction.Value);
            }
        }

        private static IEnumerable<MapObjectData> GetMines(MapData map)
            => map.Objects.Where(x => x.Mine != null);

        private static IEnumerable<MapObjectData> GetResources(MapData map)
            => map.Objects.Where(x => x.ResourcePile != null);

        private static IEnumerable<MapObjectData> GetNeutrals(MapData map)
            => map.Objects.Where(x => x.NeutralArmy != null);

        private static MapObjectData GetNearestObject(Location self, IEnumerable<MapObjectData> objects)
        {
            if (objects.Count() == 0) return null;
            var nearestLocation = objects.Min(x => self.EuclideanDistance(x.Location.ToLocation()));

            return objects.FirstOrDefault(x => self.EuclideanDistance(x.Location.ToLocation()) == nearestLocation);
        }

        private static MapObjectData GetObjectAt(MapData map, Location location)
        {
            if (location.X < 0 || location.X >= map.Width || location.Y < 0 || location.Y >= map.Height)
                return null;
            return map.Objects
                .FirstOrDefault(x => x.Location.X == location.X && x.Location.Y == location.Y);
        }

        static string GetObjectAt1(MapData map, Location location)
        {
            if (location.X < 0 || location.X >= map.Width || location.Y < 0 || location.Y >= map.Height)
                return "Outside";
            return map.Objects.
                Where(x => x.Location.X == location.X && x.Location.Y == location.Y)
                .FirstOrDefault()?.ToString() ?? "Nothing";
        }

        static void Print(HommSensorData data)
        {
            Console.WriteLine("---------------------------------");

            Console.WriteLine($"You are here: ({data.Location.X},{data.Location.Y})");

            Console.WriteLine($"You have {data.MyTreasury.Select(z => z.Value + " " + z.Key).Aggregate((a, b) => a + ", " + b)}");

            var location = data.Location.ToLocation();

            Console.Write("W: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.Up)));

            Console.Write("E: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.RightUp)));

            Console.Write("D: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.RightDown)));

            Console.Write("S: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.Down)));

            Console.Write("A: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.LeftDown)));

            Console.Write("Q: ");
            Console.WriteLine(GetObjectAt1(data.Map, location.NeighborAt(Direction.LeftUp)));
        }



        static void OnInfo(string infoMessage)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(infoMessage);
            Console.ResetColor();
        }




       
    }
}