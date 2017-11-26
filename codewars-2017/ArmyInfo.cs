using System.Collections.Generic;
using System.Linq;
using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using static System.Math;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
	public class ArmyInfo
	{
		public bool isMine;
		public long playerId;
		public NuclearStrike strike;
		public int score;
		private int lastScore;
		public GroupTask lastTask;

		private int tick;
		private StrategyType strategy;
		private bool isCrushed;
		private bool canTacticalNuclearAttack;
		private const int lossScoreFactor = 2;
		
		#region Custom Collections
		public readonly Dictionary<long, Vehicle> All = new Dictionary<long, Vehicle>(500);
		public readonly Queue<GroupProperties> NotInitializedGroups = new Queue<GroupProperties>();
		public readonly Dictionary<VehicleType, Dictionary<long, Vehicle>> ByType =
			new Dictionary<VehicleType, Dictionary<long, Vehicle>> {
				{ VehicleType.Tank, new Dictionary<long, Vehicle>(100) },
				{ VehicleType.Ifv, new Dictionary<long, Vehicle>(100) },
				{ VehicleType.Helicopter, new Dictionary<long, Vehicle>(100) },
				{ VehicleType.Fighter, new Dictionary<long, Vehicle>(100) },
				{ VehicleType.Arrv, new Dictionary<long, Vehicle>(100) }
			};
		public Dictionary<int, ISquad> Squads = new Dictionary<int, ISquad>();
		public Dictionary<int, int> ExecutedTasks = new Dictionary<int, int>();
		
		#endregion

		public void Init(World world)
		{
			var boundCoordinatesByType = new Dictionary<VehicleType, CoordinateBounds>
			{
				{ VehicleType.Tank, new CoordinateBounds{ minX = double.MaxValue, maxX = 0, minY = double.MaxValue, maxY = 0} },
				{ VehicleType.Ifv, new CoordinateBounds{ minX = double.MaxValue, maxX = 0, minY = double.MaxValue, maxY = 0} },
				{ VehicleType.Helicopter, new CoordinateBounds{ minX = double.MaxValue, maxX = 0, minY = double.MaxValue, maxY = 0} },
				{ VehicleType.Fighter, new CoordinateBounds{ minX = double.MaxValue, maxX = 0, minY = double.MaxValue, maxY = 0} },
				{ VehicleType.Arrv, new CoordinateBounds{ minX = double.MaxValue, maxX = 0, minY = double.MaxValue, maxY = 0} },
			};

			var newVehicles = world.NewVehicles;
			for (var i = 0; i < newVehicles.Length; i++)
			{
				var vehicle = newVehicles[i];
				if (vehicle.PlayerId == playerId)
				{
					All.Add(vehicle.Id, vehicle);
					if (isMine)
					{
						ByType[vehicle.Type].Add(vehicle.Id, vehicle);

						var coordinates = boundCoordinatesByType[vehicle.Type];
						coordinates.minX = Min(coordinates.minX, vehicle.X);
						coordinates.maxX = Max(coordinates.maxX, vehicle.X);
						coordinates.minY = Min(coordinates.minY, vehicle.Y);
						coordinates.maxY = Max(coordinates.maxY, vehicle.Y);
					}
				}

			}
			if (!isMine) return;
			// creating groups
			var formPoint = new Coordinate { X = world.Width, Y = world.Height };
			foreach (var item in boundCoordinatesByType.OrderBy(bc => bc.Key == VehicleType.Fighter ? 0 : bc.Key == VehicleType.Helicopter ? 1 : 2).ThenBy(
			  bc => Pow((bc.Value.minX + bc.Value.maxX) / 2 - formPoint.X, 2) + Pow((bc.Value.minY + bc.Value.maxY) / 2 - formPoint.Y, 2)))
			{
				var bounds = item.Value;
				if (item.Key == VehicleType.Fighter || item.Key == VehicleType.Helicopter)
				{
					NotInitializedGroups.Enqueue(new GroupProperties
					{
						left = bounds.minX - 3,
						right = bounds.maxX + 3,
						top = bounds.minY - 3,
						bottom = bounds.maxY + 3,
						id = NotInitializedGroups.Count + 1,
						type = item.Key,
						scaleLocation = new Coordinate { X = (bounds.maxX + bounds.minX)/2, Y = (bounds.maxY + bounds.minY)/2 }
					});
					continue;
				}
				NotInitializedGroups.Enqueue(new GroupProperties
				{
					left = bounds.minX + (bounds.maxX - bounds.minX) / 2,
					right = bounds.maxX + 3,
					top = bounds.minY + (bounds.maxY - bounds.minY) / 2,
					bottom = bounds.maxY + 3,
					id = NotInitializedGroups.Count + 1,
					type = item.Key,
					scaleLocation = new Coordinate { X = bounds.maxX + 3, Y = bounds.maxY + 3 }
				});
				NotInitializedGroups.Enqueue(new GroupProperties
				{
					left = bounds.minX + (bounds.maxX - bounds.minX) / 2,
					right = bounds.maxX + 3,
					top = bounds.minY - 3,
					bottom = bounds.minY + (bounds.maxY - bounds.minY) / 2,
					id = NotInitializedGroups.Count + 1,
					type = item.Key,
					scaleLocation = new Coordinate { X = bounds.maxX + 3, Y = bounds.minY + (bounds.maxY - bounds.minY) / 2 }
				});
				NotInitializedGroups.Enqueue(new GroupProperties
				{
					left = bounds.minX - 3,
					right = bounds.minX + (bounds.maxX - bounds.minX) / 2,
					top = bounds.minY + (bounds.maxY - bounds.minY) / 2,
					bottom = bounds.maxY + 3,
					id = NotInitializedGroups.Count + 1,
					type = item.Key,
					scaleLocation = new Coordinate { X = bounds.minX - 3, Y = bounds.maxY + 3 }
				});
				NotInitializedGroups.Enqueue(new GroupProperties
				{
					left = bounds.minX - 3,
					right = bounds.minX + (bounds.maxX - bounds.minX) / 2,
					top = bounds.minY - 3,
					bottom = bounds.minY + (bounds.maxY - bounds.minY) / 2,
					id = NotInitializedGroups.Count + 1,
					type = item.Key,
					scaleLocation = new Coordinate { X = bounds.minX + (bounds.maxX - bounds.minX) / 2, Y = bounds.minY + (bounds.maxY - bounds.minY) / 2 }
				});
			}
		}

		public void Update(World world)
		{
			BeforeUpdate();
			tick = world.TickIndex;
			var player = world.Players.First(p => p.Id == playerId);
			score = player.Score;
			isCrushed = player.IsStrategyCrashed;
			strike = (player.NextNuclearStrikeTickIndex > 0)
				? new NuclearStrike
				{
					target = new Coordinate { X = player.NextNuclearStrikeX, Y = player.NextNuclearStrikeY },
					tick = player.NextNuclearStrikeTickIndex,
					vehicleId = player.NextNuclearStrikeVehicleId
				}
				: null;
			canTacticalNuclearAttack = player.RemainingNuclearStrikeCooldownTicks == 0;
			
			foreach (var newVehicle in world.NewVehicles)
			{
				if (newVehicle.PlayerId == playerId)
				{
					All[newVehicle.Id] = newVehicle;
				}
			}

			var needDequeueNotInitializedGroupId = 0;
			foreach (var update in world.VehicleUpdates)
			{
				var id = update.Id;
				Vehicle oldVehicle;
				if (All.TryGetValue(id, out oldVehicle))
				{
					var newVehicle = new Vehicle(oldVehicle, update);
					if (newVehicle.Durability == 0)
					{
						All.Remove(id);
						ByType[newVehicle.Type].Remove(id);
						foreach (var groupId in oldVehicle.Groups)
						{
							ISquad squad;
							if (Squads.TryGetValue(groupId, out squad))
							{
								squad.RemoveVehicle(id);
							}
						}
						continue;
					}
					All[id] = newVehicle;
					ByType[oldVehicle.Type][id] = newVehicle;

					foreach (var groupId in newVehicle.Groups)
					{
						ISquad squad;
						if (Squads.TryGetValue(groupId, out squad))
						{
							squad.Vehicles[id] = newVehicle;
							// recording vehicle speed and angles of motions
							var speed = Sqrt(Pow(newVehicle.X - oldVehicle.X, 2) + Pow(newVehicle.Y - oldVehicle.Y, 2));
							if (speed > 0.1)
							{
								squad.MovingVehicles[id] = new Coordinate { X = newVehicle.X - oldVehicle.X, Y = newVehicle.Y - oldVehicle.Y };
								squad.Angles[id] = Geometry.GetAngle(oldVehicle.X, newVehicle.X, oldVehicle.Y, newVehicle.Y);
							}
							// recording fact of attack
							if (newVehicle.Durability < oldVehicle.Durability)
							{
								squad.AttackedVehicles++;
							}
						}
						else
						{
							// new group detecting ang squad creating
							var nextGroup = NotInitializedGroups.Any() ? NotInitializedGroups.Peek() : null;
							ISquad newSquad = null;
							if (nextGroup != null && nextGroup.id == groupId)
							{
								needDequeueNotInitializedGroupId = groupId;
								newSquad = Squad.Create(nextGroup.id, All, Squads, nextGroup.type);
							}
							else
							{
								newSquad = new Squad(groupId, All, Squads);
							}
							newSquad.AddOrUpdateVehicle(newVehicle);
							Squads[groupId] = newSquad;
						}
					}
					// removing vehicle from squad if it was dismissed
					foreach (var squadId in oldVehicle.Groups.Where(g => !newVehicle.Groups.Contains(g)))
					{
						Squads[squadId].RemoveVehicle(id);
					}
					
				}
			}
			AfterUpdate(needDequeueNotInitializedGroupId);
		}

		private void BeforeUpdate()
		{
			foreach (var squad in Squads)
			{
				squad.Value.MovingVehicles.Clear();
				squad.Value.Angles.Clear();
				squad.Value.Route = null;
			}
		}

		private void AfterUpdate(int needDequeueNotInitializedGroupId)
		{
			if (needDequeueNotInitializedGroupId > 0 && NotInitializedGroups.Any())
			{
				NotInitializedGroups.Dequeue();
				Squads[needDequeueNotInitializedGroupId].Init();
			}
		}

		public void Analyze(ArmyInfo opponent)
		{
			strategy = (lastScore - score) > (opponent.lastScore - opponent.score) * lossScoreFactor
				? StrategyType.Brave
				: (lastScore - score) * lossScoreFactor < (opponent.lastScore - opponent.score)
					? StrategyType.Back
					: StrategyType.Normal;
			foreach (var squad in Squads)
			{
				squad.Value.Target = null;
				squad.Value.IsUnderNuclearAttack = opponent.strike != null && squad.Value.Vehicles
				.Any(v => v.Value.GetDistanceTo(opponent.strike.target.X, opponent.strike.target.Y) < 50);
			}
			lastScore = score;
		}

		public GroupTask GetTask(ArmyInfo opponent)
		{
			Analyze(opponent);
			GroupTask task = null;
			ISquad squad = null;
			if (lastTask != null && Squads.TryGetValue(lastTask.group, out squad) && squad.Target != null 
				&& (lastTask.tick + lastTask.duration > tick && lastTask.action != ActionType.Scale 
				|| lastTask.tick + lastTask.duration > tick && lastTask.factor > 0 && lastTask.factor < 1 
				&& !squad.IsCollapsed))
			{
				return new GroupTask
				{
					action = ActionType.None
				};
			}
			if (lastTask != null && lastTask.next != null && squad != null && squad.Target != null)
			{
				
				task = lastTask.next(opponent, strategy);
				return task;
			}
			task = GetInitializationTask();
			if (task != null)
			{
				return task;
			}
			task = FormTacticalDefenseTask(opponent);
			if(task != null)
			{
				return task;
			}
			task = FormTacticalAttackTask(opponent);
			if (task != null)
			{
				return task;
			}
			task = Squads.Select(s => s.Value.FormTask(opponent, strategy)).Where(t => t != null)
				.Where(t => !NotInitializedGroups.Any() || Squads[t.group].LastTask == null)
					.OrderBy(t => t.priority).ThenBy(t => t.order)
					.ThenBy(t => Squads[t.group].LastTask != null ? Squads[t.group].LastTask.tick : int.MinValue)
					.ThenByDescending(t => (int)t.action).FirstOrDefault();
			
			return task;
		}

		private GroupTask GetInitializationTask()
		{
			if (!NotInitializedGroups.Any()) return null;
			var nextGroup = NotInitializedGroups.Peek();
			var groupedVehicles = ByType[nextGroup.type].Select(v => v.Value)
				.Where(v => v.X < nextGroup.right && v.X > nextGroup.left && v.Y < nextGroup.bottom && v.Y > nextGroup.top).ToArray();
			var isGroupSelected = groupedVehicles.All(v => v.IsSelected && !v.Groups.Any());
			if (isGroupSelected)
			{
				var scaleTask = new GroupTask
				{
					group = nextGroup.id,
					action = ActionType.Scale,
					factor = 0.1,
					duration = 30,
					X = nextGroup.scaleLocation.X,
					Y = nextGroup.scaleLocation.Y
				};
				return new GroupTask
				{
					action = ActionType.Assign,
					group = nextGroup.id,
					next = (opp, strat) => scaleTask
				};
			}
			else
			{
				return new GroupTask
				{
					action = ActionType.ClearAndSelect,
					left = nextGroup.left,
					top = nextGroup.top,
					right = nextGroup.right,
					bottom = nextGroup.bottom,
				};
			}
		}

		private GroupTask FormTacticalAttackTask(ArmyInfo opponent)
		{
			if (!canTacticalNuclearAttack)
			{
				return null;
			}
			GroupTask task = null;
			if (opponent.Squads.Any())
			{
				var target = opponent.Squads.Select(s => s.Value.Target).Where(t => t != null)
					.Where(t => All.Any(v => 
					{
						var range = v.Value.GetDistanceTo(t.center.X, t.center.Y);
						return range < 0.95 * v.Value.VisionRange && range > 0.7 * v.Value.VisionRange;
					})).OrderByDescending(t => (int)(t.groundDamage / 10))
					.ThenBy(t => All.Where(a => a.Value.GetDistanceTo(t.center.X, t.center.Y) < 50)
						.Sum(a => 99d - 99d / 50 * a.Value.GetDistanceTo(t.center.X, t.center.Y)) / 10)
					.ThenBy(t=> t.type == VehicleType.Tank 
						? 0 : t.type == VehicleType.Ifv 
						? 1 : t.type == VehicleType.Helicopter 
						? 2 : t.type == VehicleType.Fighter ? 3 : 4).FirstOrDefault();
				if (target != null) {
					task = new GroupTask
					{
						action = ActionType.TacticalNuclearStrike,
						order = 1,
						priority = 0,
						X = target.center.X,
						Y = target.center.Y,
						vehicleId = All.First(v => 
						{
							var range = v.Value.GetDistanceTo(target.center.X, target.center.Y);
							return range < 0.95 * v.Value.VisionRange && range > 0.7 * v.Value.VisionRange;
						}).Value.Id,
						duration = 30
					};
				}
			}
			var nearestVehicle = opponent.All.Where(v => All.Any(s => 
				{
					var range = s.Value.GetDistanceTo(v.Value.X, v.Value.Y);
					return range < 0.95 * s.Value.VisionRange && range > 0.7 * s.Value.VisionRange;
				})).OrderByDescending(v => opponent.All.Where(a => a.Value.GetDistanceTo(v.Value) < 50).Sum(a => a.Value.GroundDamage)/10)
				.ThenBy(v => All.Where(a => a.Value.GetDistanceTo(v.Value) < 50).Sum(a => 99d - 99d/50*a.Value.GetDistanceTo(v.Value)) / 10)
					.ThenBy(v => v.Value.Type == VehicleType.Tank
						? 0 : v.Value.Type == VehicleType.Ifv
						? 1 : v.Value.Type == VehicleType.Helicopter
						? 2 : v.Value.Type == VehicleType.Fighter ? 3 : 4).FirstOrDefault();
			task = nearestVehicle.Value != null 
				? new GroupTask
				{
					action = ActionType.TacticalNuclearStrike,
					order = 1,
					priority = 0,
					X = nearestVehicle.Value.X,
					Y = nearestVehicle.Value.Y,
					vehicleId = All.First(v => 
					{
						var range = v.Value.GetDistanceTo(nearestVehicle.Value.X, nearestVehicle.Value.Y);
						return range < 0.95 * v.Value.VisionRange && range > 0.7 * v.Value.VisionRange;
					}).Value.Id,
					duration = 30
				} : null;
			if (task == null)
			{
				return null;
			}
			var squad = Squads.First(s => s.Value.Vehicles.ContainsKey(task.vehicleId)).Value;
			var stopTask = new GroupTask
				{
					action = ActionType.Move,
					X = 0,
					Y = 0,
					group = squad.Id,
					next = ( opp, strat) => task
				};
			return squad.Vehicles.Any(v => !v.Value.IsSelected) 
				? new GroupTask
				{
					action = ActionType.ClearAndSelect,
					group = squad.Id,
					next = (opp, strat) => stopTask
				} : stopTask;
		}

		private GroupTask FormTacticalDefenseTask(ArmyInfo opponent)
		{
			var squadsUnderNuclearAttack = Squads.Where(s => s.Value.IsUnderNuclearAttack).ToArray();
			if(!squadsUnderNuclearAttack.Any())
			{
				return null;
			}
			var scaleInTask = new GroupTask
			{
				priority = 0,
				order = 0,
				duration = 30,
				action = ActionType.Scale,
				factor = 0.1,
				X = opponent.strike.target.X,
				Y = opponent.strike.target.Y
			};
			var scaleOutTask = new GroupTask
			{
				priority = 0,
				order = 0,
				duration = 30,
				action = ActionType.Scale,
				factor = 10,
				next = (opp, strat) => scaleInTask,
				X = opponent.strike.target.X,
				Y = opponent.strike.target.Y
			};
			
			var task = squadsUnderNuclearAttack.Length > 1 ? new GroupTask
			{
				action = ActionType.ClearAndSelect,
				top = squadsUnderNuclearAttack.Min(s => s.Value.Vehicles.Min(v => (int)v.Value.Y)),
				bottom = squadsUnderNuclearAttack.Min(s => s.Value.Vehicles.Max(v => (int)v.Value.Y)),
				left = squadsUnderNuclearAttack.Min(s => s.Value.Vehicles.Min(v => (int)v.Value.X)),
				right = squadsUnderNuclearAttack.Min(s => s.Value.Vehicles.Max(v => (int)v.Value.X)),
				next = (opp, strat) => scaleOutTask
			} : !squadsUnderNuclearAttack.First().Value.Vehicles.All(v => v.Value.IsSelected) ? new GroupTask
			{
				action = ActionType.ClearAndSelect,
				group = squadsUnderNuclearAttack.First().Value.Id,
				next = (opp, strat) => scaleOutTask
			} : scaleOutTask;
			return task;
		}

		public void SetLastTask(GroupTask task)
		{
			task.tick += tick;
			lastTask = task;
			ISquad squad;
			if (task.action != ActionType.TacticalNuclearStrike
				&& task.action != ActionType.SetupVehicleProduction && task.group > 0 && Squads.TryGetValue(task.group, out squad))
			{
				squad.LastTask = task;
			}
		}
	}
}
