using System.Collections.Generic;
using System.Linq;
using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using static System.Math;
using System;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
	public interface ISquad
	{
		int Id { get;}
		Dictionary<long, Vehicle> Vehicles { get; }
		Target Target { get; set; }
		VehicleType Type { get; }
		GroupTask LastTask { get; set; }
		bool IsCollapsed { get; }
		bool IsUnderNuclearAttack { get; set; }
		bool IsUnderAttack { get; }
		bool IsAllMoved { get; }
		Dictionary<long, double> MovingVehicles { get; }
		int AttackedVehicles { get; set; }
		Dictionary<long, double> Angles { get; }
		Dictionary<long, Vehicle> All { get; }
		Dictionary<int, ISquad> Squads { get; }
		Route Route { get; }
		void AddOrUpdateVehicle(Vehicle vehicle);
		void RemoveVehicle(long id);
		GroupTask FormTask(ArmyInfo opponent, StrategyType strategy);
		void Init();
	}

	public class Squad : ISquad
	{
		public int Id { get; private set; }
		public Dictionary<long, Vehicle> Vehicles => vehicles;
		public VehicleType Type => type ?? Vehicles.GroupBy(v => v.Value.Type).OrderByDescending(g => g.ToArray().Length).First().Key;
		public GroupTask LastTask { get; set; }
		public bool IsUnderNuclearAttack { get; set; }
		public bool IsUnderAttack => AttackedVehicles > 0;
		public bool IsCollapsed => Target.variance < 0.9 * initVariance;
		public bool IsAllMoved => MovingVehicles.Count == Vehicles.Count;
		public Dictionary<long, double> MovingVehicles => movingVehicles;
		public int AttackedVehicles { get; set; }
		public Dictionary<long, double> Angles => angles;
		public Dictionary<long, Vehicle> All { get; }
		public Dictionary<int, ISquad> Squads { get; }
		public Route Route => Route;

		private Route route;
		private readonly Dictionary<long, double> movingVehicles = new Dictionary<long, double>();
		private readonly Dictionary<long, double> angles = new Dictionary<long, double>();
		private readonly Dictionary<long, Vehicle> vehicles = new Dictionary<long, Vehicle>();
		private readonly TerrainType[][] terrainByCellXY;
		private readonly WeatherType[][] weatherByCellXY;
		private Target target;
		protected VehicleType? type;
		protected double initVariance;
		protected double rangeToTarget;
		protected double lastRangeToTarget;
		protected Coordinate lastTargetCoordinate;
		private const int rangePortionOrdering = 20;
		private const int anticipationTicksInterval = 60;


		public Squad(int id, Dictionary<long, Vehicle> all, Dictionary<int, ISquad> squads, VehicleType? type = null)
		{
			Id = id;
			this.type = type;
			All = all;
			Squads = squads;
		}

		public void AddOrUpdateVehicle(Vehicle vehicle)
		{
			Vehicles[vehicle.Id] = vehicle;
		}

		public void RemoveVehicle(long id)
		{
			Vehicles.Remove(id);
		}

		public void Init()
		{
			initVariance = Target.variance;
		}

		public GroupTask FormTask(ArmyInfo opponent, StrategyType strategy)
		{
			var task = GetTaskByStrategy(opponent, strategy);
			
			if (Target != null && Target.variance > 1.5 * initVariance)
			{
				//var scaletask = CreateScaleTask(0.1, (opp, strat) => GetTaskByStrategy(opp, strat), task.priority, task.order, 10);
				//var rotatetask = CreateRotateTask(-PI / 4, (opp, strat) => scaletask, task.priority, task.order, 10);
				//var stopTask = CreateMoveTask(0, 0, (opp, strat) => rotatetask, task.priority, task.order);
				//task = CreateScaleTask(0.1, (opp, strat) => stopTask, task.priority, task.order, 10);
				task = CreateScaleTask(0.1, (opp, strat) => GetTaskByStrategy(opp, strat), task.priority, task.order, 10);
			}
			if (task != null && Vehicles.Any(v => !v.Value.IsSelected))
			{
				return CreateSelectTask((opp, strat) => GetTaskByStrategy(opponent, strategy), task.priority, task.order); 
			}
			
			return task;
		}

		public GroupTask GetTaskByStrategy(ArmyInfo opponent, StrategyType strategy)
		{
			if (Target == null) return null;
			GroupTask task = null;
			if (strategy == StrategyType.Brave)
			{
				task = GetBraveMoveTask(opponent);
			}
			else if (strategy == StrategyType.Back)
			{
				task = GetBackMoveTask(opponent);
			}
			else
			{
				task = GetCarefullMoveTask(opponent);
			}
			return task;
		}

		public virtual bool FilterSquad(ISquad squad)
		{
			return true;
		}

		public virtual bool FilterVehicle(Vehicle vehicle)
		{
			return true;
		}

		public virtual bool FilterTactical(Vehicle vehicle)
		{
			return true;
		}

		public virtual int OrderByTargetType(Target target)
		{
			return target.type == VehicleType.Arrv
						? 0 : target.type == VehicleType.Ifv
						? 1 : target.type == VehicleType.Tank
						? 2 : target.type == VehicleType.Fighter ? 3 : 4;
		}

		public virtual int OrderByVehicleType(Vehicle vehicle)
		{
			return vehicle.Type == VehicleType.Arrv
						? 0 : vehicle.Type == VehicleType.Ifv
						? 1 : vehicle.Type == VehicleType.Tank
						? 2 : vehicle.Type == VehicleType.Fighter ? 3 : 4;
		}

		public virtual GroupTask GetBraveMoveTask(ArmyInfo opponent)
		{
			var target = opponent.Squads.Select(s => s.Value).Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			if (target != null && target.variance > 2 * initVariance)
			{
				return CreateMoveTask(target.center.X - Target.center.X, target.center.Y - Target.center.Y);
			}
			var nearestVehicle = opponent.All.Select(v => v.Value).Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearestVehicle != null)
			{
				return CreateMoveTask(nearestVehicle.X - Target.center.X, nearestVehicle.Y - Target.center.Y);
			}
			var tacticalTarget = opponent.All.Select(v => v.Value).Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				var apoint = FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
					type == VehicleType.Fighter ? 120 : type == VehicleType.Helicopter ? 100 : type == VehicleType.Arrv ? 60 : 80);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			return null;
		}

		public virtual GroupTask GetCarefullMoveTask(ArmyInfo opponent)
		{
			var target = opponent.Squads.Select(s => s.Value).Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2))/ rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			Coordinate apoint;
			if (target != null && target.variance > 2 * initVariance)
			{
				apoint = FindAttackingPoint(target.center.X, target.center.Y, Target.center.X, Target.center.Y, 20);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			var nearest = opponent.All.Select(v => v.Value).Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearest != null)
			{
				apoint = FindAttackingPoint(nearest.X, nearest.Y, Target.center.X, Target.center.Y, 20);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			var tacticalTarget = opponent.All.Select(v => v.Value).Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				apoint = FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
				type == VehicleType.Fighter ? 120 : type == VehicleType.Helicopter ? 100 : type == VehicleType.Arrv ? 60 : 80);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			return null;
		}

		public virtual GroupTask GetBackMoveTask(ArmyInfo opponent)
		{
			var target = opponent.Squads.Select(s => s.Value).Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			Coordinate apoint;
			if (target != null && target.variance > 2 * initVariance)
			{
				apoint = FindAttackingPoint(target.center.X, target.center.Y, Target.center.X, Target.center.Y, 
					type == VehicleType.Fighter ? 120 : type == VehicleType.Helicopter ? 100 : type == VehicleType.Arrv ? 60 : 80);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			var nearest = opponent.All.Select(v => v.Value).Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y)/rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearest != null)
			{
				apoint = FindAttackingPoint(nearest.X, nearest.Y, Target.center.X, Target.center.Y,
					type == VehicleType.Fighter ? 120 : type == VehicleType.Helicopter ? 100 : type == VehicleType.Arrv ? 60 : 80);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			var tacticalTarget = opponent.All.Select(v => v.Value).Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				apoint = FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
					type == VehicleType.Fighter ? 120 : type == VehicleType.Helicopter ? 100 : type == VehicleType.Arrv ? 60 : 80);
				return CreateMoveTask(apoint.X, apoint.Y);
			}
			return null;
		}

		private Coordinate FindAttackingPoint(double targetX, double targetY, double selfX, double selfY, double range)
		{
			var angle = Atan((targetY - selfY) / (targetX - selfX));
			return new Coordinate { X = targetX - selfX - 0.8 * range * Cos(angle), Y = targetY - selfY - 0.8 * range * Sin(angle) };
		}

		private GroupTask CreateMoveTask(double x, double y, Func<ArmyInfo, StrategyType, GroupTask> next = null, int? priority = null, int? order = null, int duration = 0)
		{
			double maxSpeed = GetMaxSpeed();
			var targetpoint = CorrectPoint(x, y, ref maxSpeed);
			var targetMovingDelta = lastTargetCoordinate != null 
				? Sqrt(Pow(x - lastTargetCoordinate.X, 2) + Pow(y - lastTargetCoordinate.Y, 2)) : int.MaxValue;
			lastRangeToTarget = rangeToTarget;
			rangeToTarget = Sqrt(Pow(x - Target.center.X, 2) + Pow(y - Target.center.Y, 2));
			return new GroupTask
			{
				X = targetpoint.X,
				Y = targetpoint.Y,
				action = ActionType.Move,
				group = Id,
				order = order ?? (rangeToTarget == 0 || targetMovingDelta / rangeToTarget > 0.2 
					? !IsAllMoved || rangeToTarget < 2 * Target.variance ? 0 : 1 
					: !IsAllMoved || rangeToTarget < 2 * Target.variance ? 1 : 2),
				priority = priority ?? (IsUnderAttack || !IsAllMoved ? 1 : 2),
				vehicleType = type,
				duration = duration > 0 ? duration : 5,
				maxSpeed = maxSpeed // todo implement maxspeed accounting
			};
		}

		private double GetMaxSpeed()
		{
			return 0;
		}

		private Coordinate CorrectPoint(double x, double y, ref double maxSpeed)
		{
			var newRoute = new Route(Vehicles, Target.center.X, Target.center.Y, x, y);
			return new Coordinate { X = x, Y = y };
			if (Squads.Where(s => s.Key != Id).Select(s => s.Value.Route).Any(r => CollapseWith(r, x, y)))
			{

			}
		}

		private bool CollapseWith(Route route, double x, double y)
		{
			route.Any(v => v.Value)
		}

		private GroupTask CreateScaleTask(double factor, Func<ArmyInfo, StrategyType, GroupTask> next, int priority, int order, int duration = 30)
		{
			return new GroupTask
			{
				action = ActionType.Scale,
				group = Id,
				factor = factor,
				X = Target.center.X,
				Y = Target.center.Y,
				duration = duration,
				next = next,
				priority = priority,
				order = order
			};
		}

		private GroupTask CreateRotateTask(double angle, Func<ArmyInfo, StrategyType, GroupTask> next, int priority, int order, int duration = 30)
		{
			return new GroupTask
			{
				action = ActionType.Rotate,
				group = Id,
				angle = angle,
				X = Target.center.X,
				Y = Target.center.Y,
				duration = duration,
				next = next,
				priority = priority,
				order = order
			};
		}

		private GroupTask CreateSelectTask(Func<ArmyInfo, StrategyType, GroupTask> next, int priority, int order)
		{
			return new GroupTask
			{
				action = ActionType.ClearAndSelect,
				group = Id,
				next = next,
				priority = priority,
				order = order
			};
		}

		public static ISquad Create(int id, Dictionary<long, Vehicle> all, Dictionary<int, ISquad> squads, VehicleType type)
		{
			switch (type)
			{
				case VehicleType.Fighter:
				case VehicleType.Helicopter:
					return new AirSquad(id, all, squads, type);
				case VehicleType.Arrv:
					return new ArrvSquad(id, all, squads, type);
				default:
					return new Squad(id, all, squads, type);
			}
		}

		public Target Target
		{
			get
			{
				if (target != null)
				{
					return target;
				}
				if (!Vehicles.Any())
				{
					return null;
				}
				var center = new Coordinate { X = Vehicles.Average(v => v.Value.X), Y = Vehicles.Average(v => v.Value.Y) };
				var airDamage = 0d;
				var groundDamage = 0d;
				var nucleardamage = 0d;
				var sumspeed = 0d;
				var totalDurability = 0;
				var variance = 0d;
				foreach (var item in Vehicles)
				{
					var vehicle = item.Value;
					airDamage += vehicle.AerialDamage;
					groundDamage += vehicle.GroundDamage;
					nucleardamage += (1 - (vehicle.GetDistanceTo(center.X, center.Y) / 50)) * 99d;
					sumspeed += vehicle.MaxSpeed;
					totalDurability += vehicle.Durability;
					variance += Pow(vehicle.GetDistanceTo(center.X, center.Y), 2);
				}
				return target = new Target
				{
					airDamage = airDamage,
					center = center,
					groundDamage = groundDamage,
					nuclearDamage = nucleardamage,
					speed = sumspeed / Vehicles.Count,
					strength = Vehicles.Count,
					totalDurability = totalDurability,
					variance = Sqrt(variance / Vehicles.Count),
					type = Type
				};
			}
			set
			{
				target = value;
			}
		}
	}

	public class ArrvSquad : Squad
	{
		public ArrvSquad(int id, Dictionary<long, Vehicle> all, Dictionary<int, ISquad> squads, VehicleType type) : base(id, all, squads, type)
		{ }

		
	}

	public class AirSquad : Squad
	{
		public AirSquad(int id, Dictionary<long, Vehicle> all, Dictionary<int, ISquad> squads, VehicleType type) : base(id, all, squads, type)
		{ }

		public override bool FilterSquad(ISquad squad)
		{
			return type == VehicleType.Helicopter
				|| squad.Type == VehicleType.Helicopter || squad.Type == VehicleType.Fighter;
		}
		public override bool FilterVehicle(Vehicle vehicle)
		{
			return type == VehicleType.Helicopter
					|| vehicle.Type == VehicleType.Helicopter || vehicle.Type == VehicleType.Fighter;
		}

		public override int OrderByTargetType(Target target)
		{
			return type == VehicleType.Helicopter ? target.type == VehicleType.Arrv
						? 0 : target.type == VehicleType.Tank
						? 1 : target.type == VehicleType.Helicopter
						? 2 : target.type == VehicleType.Ifv ? 3 : 4
					: target.type == VehicleType.Helicopter
						? 0 : target.type == VehicleType.Fighter
						? 1 : target.type == VehicleType.Ifv
						? 2 : target.type == VehicleType.Tank ? 3 : 4;
		}

		public override int OrderByVehicleType(Vehicle vehicle)
		{
			return type == VehicleType.Helicopter ? vehicle.Type == VehicleType.Arrv
						? 0 : vehicle.Type == VehicleType.Tank
						? 1 : vehicle.Type == VehicleType.Helicopter
						? 2 : vehicle.Type == VehicleType.Ifv ? 3 : 4
					: vehicle.Type == VehicleType.Helicopter
						? 0 : vehicle.Type == VehicleType.Fighter
						? 1 : vehicle.Type == VehicleType.Ifv
						? 2 : vehicle.Type == VehicleType.Tank ? 3 : 4;
		}

	}
}
