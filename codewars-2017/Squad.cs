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
		Dictionary<long, Coordinate> MovingVehicles { get; }
		int AttackedVehicles { get; set; }
		Dictionary<long, double> Angles { get; }
		Dictionary<long, Vehicle> All { get; }
		Dictionary<int, ISquad> Squads { get; }
		Route Route { get; set; }
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
		public Dictionary<long, Coordinate> MovingVehicles => movingVehicles;
		public int AttackedVehicles { get; set; }
		public Dictionary<long, double> Angles => angles;
		public Dictionary<long, Vehicle> All { get; }
		public Dictionary<int, ISquad> Squads { get; }
		public int VisionRange => visionRange > 0 
			? visionRange 
			: (visionRange = Type == VehicleType.Fighter ? 120 : Type == VehicleType.Helicopter ? 100 : Type == VehicleType.Arrv ? 60 : 80);
		public int AttackRange => visionRange > 0
			? visionRange
			: (visionRange = Type == VehicleType.Fighter ? 120 : Type == VehicleType.Helicopter ? 100 : Type == VehicleType.Arrv ? 60 : 80);

		private Route route;
		private readonly Dictionary<long, Coordinate> movingVehicles = new Dictionary<long, Coordinate>();
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
		protected const int rangePortionOrdering = 20;
		protected const int anticipationTicksInterval = 60;
		private int visionRange;
		private int atackRange;

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

			GroupTask scaleTask = null;
			if (task != null && Target != null && Target.variance > 1.5 * initVariance)
			{
				//var scaletask = CreateScaleTask(0.1, (opp, strat) => GetTaskByStrategy(opp, strat), task.priority, task.order, 10);
				//var rotatetask = CreateRotateTask(-PI / 4, (opp, strat) => scaletask, task.priority, task.order, 10);
				//var stopTask = CreateMoveTask(0, 0, (opp, strat) => rotatetask, task.priority, task.order);
				//task = CreateScaleTask(0.1, (opp, strat) => stopTask, task.priority, task.order, 10);
				scaleTask = CreateScaleTask(0.1, (opp, strat) => GetTaskByStrategy(opp, strat), task.priority, task.order, (int)Min(10, Max(60, Target.variance / initVariance * 10d)));
			}
			if (task != null && Vehicles.Any(v => !v.Value.IsSelected))
			{
				return CreateSelectTask((opp, strat) => scaleTask ?? GetTaskByStrategy(opponent, strategy), task.priority, task.order);
			}

			return scaleTask ?? task;
		}

		public GroupTask GetTaskByStrategy(ArmyInfo opponent, StrategyType strategy)
		{
			if (Target == null) return null;
			Coordinate coordinate = null;
			if (strategy == StrategyType.Brave)
			{
				coordinate = GetBraveMoveTarget(opponent);
			}
			else if (strategy == StrategyType.Back)
			{
				coordinate = GetBackMoveTarget(opponent);
			}
			else
			{
				coordinate = GetCarefullMoveTarget(opponent);
			}
			if(coordinate == null)
			{
				return null;
			}
			var task = CreateMoveTask(coordinate);
			return task;
		}

		public virtual bool FilterSquad(ISquad squad)
		{
			return squad.Type == VehicleType.Tank || squad.Type == VehicleType.Arrv || squad.Type == VehicleType.Ifv;
		}

		public virtual bool FilterVehicle(Vehicle vehicle)
		{
			return vehicle.Type == VehicleType.Tank || vehicle.Type == VehicleType.Arrv || vehicle.Type == VehicleType.Ifv;
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

		public virtual Coordinate GetBraveMoveTarget(ArmyInfo opponent)
		{
			var target = opponent.Squads.Values.Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			if (target != null && target.variance > 2 * initVariance)
			{
				return new Coordinate(target.center.X - Target.center.X, target.center.Y - Target.center.Y);
			}
			var nearestVehicle = opponent.All.Values.Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearestVehicle != null)
			{
				return new Coordinate(nearestVehicle.X - Target.center.X, nearestVehicle.Y - Target.center.Y);
			}
			var tacticalTarget = opponent.All.Values.Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				var apoint = FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
					VisionRange);
				return new Coordinate(apoint.X, apoint.Y);
			}
			return null;
		}

		public virtual Coordinate GetCarefullMoveTarget(ArmyInfo opponent)
		{
			var target = opponent.Squads.Values.Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			Coordinate apoint;
			if (target != null && target.variance > 2 * initVariance)
			{
				return FindAttackingPoint(target.center.X, target.center.Y, Target.center.X, Target.center.Y, AttackRange);
			}
			var nearest = opponent.All.Values.Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearest != null)
			{
				return FindAttackingPoint(nearest.X, nearest.Y, Target.center.X, Target.center.Y, AttackRange);
			}
			var tacticalTarget = opponent.All.Values.Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				return FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
				VisionRange);
			}
			return null;
		}

		public virtual Coordinate GetBackMoveTarget(ArmyInfo opponent)
		{
			var target = opponent.Squads.Values.Where(FilterSquad).Select(s => s.Target).Where(t => t != null)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			Coordinate apoint;
			if (target != null && target.variance > 2 * initVariance)
			{
				return FindAttackingPoint(target.center.X, target.center.Y, Target.center.X, Target.center.Y,
					VisionRange);
			}
			var nearest = opponent.All.Values.Where(FilterVehicle)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (nearest != null)
			{
				return FindAttackingPoint(nearest.X, nearest.Y, Target.center.X, Target.center.Y,
					VisionRange);
			}
			var tacticalTarget = opponent.All.Values.Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				return FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
					VisionRange);
			}
			return null;
		}

		protected Coordinate FindAttackingPoint(double targetX, double targetY, double selfX, double selfY, double range)
		{
			var angle = Atan((targetY - selfY) / (targetX - selfX));
			return new Coordinate { X = targetX - selfX - 0.8 * range * Cos(angle), Y = targetY - selfY - 0.8 * range * Sin(angle) };
		}

		private GroupTask CreateMoveTask(Coordinate coordinate, Func<ArmyInfo, StrategyType, GroupTask> next = null, int? priority = null, int? order = null, int duration = 0)
		{
			double maxSpeed = GetMaxSpeed();
			if(Target.center.X + coordinate.X < 0)
			{
				coordinate.X = Abs(coordinate.X);
			}
			if (Target.center.Y + coordinate.Y < 0)
			{
				coordinate.Y = Abs(coordinate.Y);
			}
			if (Target.center.X + coordinate.X > 1032)
			{
				coordinate.X = -Abs(coordinate.X);
			}
			if (Target.center.Y + coordinate.Y > 1032)
			{
				coordinate.Y = -Abs(coordinate.Y);
			}
			var targetpoint = CorrectPoint(coordinate.X, coordinate.Y, ref maxSpeed);
			if (targetpoint == null) return null;
			var targetMovingDelta = lastTargetCoordinate != null
				? Sqrt(Pow(coordinate.X - lastTargetCoordinate.X, 2) + Pow(coordinate.Y - lastTargetCoordinate.Y, 2)) : int.MaxValue;
			lastRangeToTarget = rangeToTarget;
			rangeToTarget = Sqrt(Pow(coordinate.X - Target.center.X, 2) + Pow(coordinate.Y - Target.center.Y, 2));
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
			return Target.speed;
		}

		protected virtual bool FilterNeighbors(ISquad squad)
		{
			return squad.Id != Id && squad.Type != VehicleType.Helicopter && squad.Type != VehicleType.Fighter;
		}

		private Coordinate CorrectPoint(double x, double y, ref double maxSpeed)
		{
			Route newRoute;
			var currentRoutes = Squads.Values.Where(FilterNeighbors).Select(s => s.Route).Where(r => r != null).ToArray();
			var speedVectors = Enumerable.Range(-18, 36).Select(n => 2 * PI / 36 * n).OrderBy(n => Abs(n))
				.Select(a => new Coordinate(x * Cos(a) - y * Sin(a), x * Sin(a) + y * Cos(a))).ToArray();
			var speedCoefficients = new[] { 1, 0.9, 0.8, 0.7, 0.6 };
			foreach(var speedVector in speedVectors)
			{
				var normalizedX = speedVector.X / (Sqrt(Pow(speedVector.X, 2) + Pow(speedVector.Y, 2))) * maxSpeed;
				var normalizedY = speedVector.Y / (Sqrt(Pow(speedVector.X, 2) + Pow(speedVector.Y, 2))) * maxSpeed;
				foreach (var coef in speedCoefficients)
				{
					newRoute = CreateRoute(normalizedX * coef, normalizedY * coef);
					if (currentRoutes.Any(r => newRoute.IsCrossingWith(r)))
					{
						continue;
					}
					maxSpeed *= coef;
					return speedVector.X == x && speedVector.Y == y ? new Coordinate(x, y)
						: new Coordinate(normalizedX * anticipationTicksInterval, normalizedY * anticipationTicksInterval);
				}
			}
			return new Coordinate(x, y);
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

		public Route Route
		{
			get
			{
				if (route != null)
				{
					return route;
				}
				var speedVector = new Coordinate();
				if (MovingVehicles.Any())
				{
					speedVector.X = MovingVehicles.Average(v => v.Value.X) / Vehicles.Count;
					speedVector.Y = MovingVehicles.Average(v => v.Value.Y) / Vehicles.Count;
				}
				return route = CreateRoute(speedVector.X, speedVector.Y);
			}
			set
			{
				route = value;
			}
		}

		public Route CreateRoute(double speedVectorX, double speedVectorY)
		{
			if (Target == null) return null;
			var front = new Coordinate();
			var rear = new Coordinate();
			var left = new Coordinate();
			var right = new Coordinate();
			var maxfrontRange = 0d;
			var maxrearRange = 0d;
			var maxleftRange = 0d;
			var maxrightRange = 0d;
			var sumAnticipationX = speedVectorX  * anticipationTicksInterval;
			var sumAnticipationY = speedVectorY * anticipationTicksInterval;
			foreach (var item in Vehicles)
			{
				var vehicle = item.Value;
				var angleFromMovingDirection = Atan2(vehicle.Y - Target.center.Y, vehicle.X - Target.center.X) - Atan2(sumAnticipationY, sumAnticipationX);
				if (angleFromMovingDirection < -PI) angleFromMovingDirection += PI * 2;
				if (angleFromMovingDirection > PI) angleFromMovingDirection -= 2 * PI;
				var rangeFromCenter = Sqrt(Pow(Target.center.X - vehicle.X, 2) + Pow(Target.center.Y - vehicle.Y, 2));
				var transversalRange = Sin(angleFromMovingDirection) * rangeFromCenter;
				var longitudinalRange = Cos(angleFromMovingDirection) * rangeFromCenter;
				if (transversalRange < maxleftRange)
				{
					maxleftRange = transversalRange;
					left.X = vehicle.X;
					left.Y = vehicle.Y;
				}
				if (transversalRange > maxrightRange)
				{
					maxrightRange = transversalRange;
					right.X = vehicle.X;
					right.Y = vehicle.Y;
				}
				if (longitudinalRange > maxfrontRange)
				{
					maxfrontRange = transversalRange;
					front.X = vehicle.X;
					front.Y = vehicle.Y;
				}
				if (longitudinalRange < maxrearRange)
				{
					maxrearRange = transversalRange;
					rear.X = vehicle.X;
					rear.Y = vehicle.Y;
				}
			}
			return route = new Route(Target.center, new Coordinate { X = sumAnticipationX, Y = sumAnticipationY }, front, rear, left, right);
		}
	}

	public class ArrvSquad : Squad
	{
		public ArrvSquad(int id, Dictionary<long, Vehicle> all, Dictionary<int, ISquad> squads, VehicleType type) : base(id, all, squads, type)
		{ }

		public override Coordinate GetBraveMoveTarget(ArmyInfo opponent)
		{
			return GetArrvTask(opponent);
		}

		public override Coordinate GetCarefullMoveTarget(ArmyInfo opponent)
		{
			return GetArrvTask(opponent);
		}

		public override Coordinate GetBackMoveTarget(ArmyInfo opponent)
		{
			return GetArrvTask(opponent);
		}

		public override bool FilterSquad(ISquad squad)
		{
			return squad.Id != Id && squad.Target != null && squad.Target.totalDurability < squad.Vehicles.Count * 100 * 0.8;
		}

		private Coordinate GetArrvTask(ArmyInfo opponent)
		{
			var target = Squads.Values.Where(FilterSquad).Select(s => s.Target)
					.OrderBy(t => (int)(Pow(t.center.X - Target.center.X, 2) + Pow(t.center.Y - Target.center.Y, 2)) / rangePortionOrdering)
					.ThenBy(OrderByTargetType)
					.FirstOrDefault();
			if (target != null)
			{
				return new Coordinate(target.center.X - Target.center.X, target.center.Y - Target.center.Y);
			}

			var tacticalTarget = opponent.All.Values.Where(FilterTactical)
				.OrderBy(v => (int)v.GetDistanceTo(Target.center.X, Target.center.Y) / rangePortionOrdering).ThenBy(OrderByVehicleType).FirstOrDefault();
			if (tacticalTarget != null)
			{
				var apoint = FindAttackingPoint(tacticalTarget.X, tacticalTarget.Y, Target.center.X, Target.center.Y,
					VisionRange);
				return new Coordinate(apoint.X, apoint.Y);
			}
			return null;
		}
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

		protected override bool FilterNeighbors(ISquad squad)
		{
			return squad.Id != Id && (squad.Type == VehicleType.Helicopter || squad.Type == VehicleType.Fighter);
		}
	}
}
