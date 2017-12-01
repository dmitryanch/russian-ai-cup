using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using static Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Geometry;
using static System.Math;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{

	public class Coordinate
	{
		public double X;
		public double Y;

		public Coordinate(double x, double y)
		{
			X = x;
			Y = y;
		}
		public Coordinate() : this(0, 0) { }
		public static Coordinate operator + (Coordinate c1, Coordinate c2)
		{
			return new Coordinate { X = c1.X + c2.X, Y = c1.Y + c2.Y };
		}
	}

	public class CoordinateBounds
	{
		public double minX;
		public double maxX;
		public double minY;
		public double maxY;
	}

	public class GroupProperties
	{
		public double left;
		public double right;
		public double top;
		public double bottom;
		public int id;
		public VehicleType type;
		public Coordinate scaleLocation;
	}

	public class GroupTask
	{
		public double left;
		public double right;
		public double top;
		public double bottom;
		public int priority;
		public int order;
		public ActionType action;
		public int group;
		public double X;
		public double Y;
		public double angle;
		public double factor;
		public double maxSpeed;
		public double maxAngularSpeed;
		public VehicleType? vehicleType;
		public long facilityId = -1L;
		public long vehicleId = -1L;
		public int tick;
		public Func<ArmyInfo, StrategyType, GroupTask> next;
		public int duration;
	}

	public class Target
	{
		public int strength;
		public Coordinate center;
		public double variance;
		public double nuclearDamage;
		public double ownNuclearDamage;
		public double groundDamage;
		public double airDamage;
		public int totalDurability;
		public double speed;
		public VehicleType type;

		public static Target Create(List<Vehicle> vehicles, VehicleType? type = null)
		{
			var center = new Coordinate { X = vehicles.Average(v => v.X), Y = vehicles.Average(v => v.Y) };
			var airDamage = 0d;
			var groundDamage = 0d;
			var nucleardamage = 0d;
			var sumspeed = 0d;
			var totalDurability = 0;
			var variance = 0d;
			foreach (var item in vehicles)
			{
				var vehicle = item;
				airDamage += vehicle.AerialDamage;
				groundDamage += vehicle.GroundDamage;
				nucleardamage += (1 - (vehicle.GetDistanceTo(center.X, center.Y) / 50)) * 99d;
				sumspeed += vehicle.MaxSpeed;
				totalDurability += vehicle.Durability;
				variance += Pow(vehicle.GetDistanceTo(center.X, center.Y), 2);

			}
			return new Target
			{
				airDamage = airDamage,
				center = center,
				groundDamage = groundDamage,
				nuclearDamage = nucleardamage,
				speed = sumspeed / vehicles.Count,
				strength = vehicles.Count,
				totalDurability = totalDurability,
				variance = Sqrt(variance / vehicles.Count),
				type = type ?? vehicles.GroupBy(v => v.Type).OrderByDescending(g => g.ToArray().Length).First().Key
			};
		}
	}

	public enum StrategyType
	{
		Normal = 0,
		Brave = 1,
		Back = 2
	}

	public class NuclearStrike
	{
		public Coordinate target;
		public int tick;
		public long vehicleId;
	}

	public class Route
	{
		public Coordinate speedVector;
		public Coordinate center;
		public Coordinate front;
		public Coordinate frontStart;
		public Coordinate rear;
		public Coordinate rearStart;
		public Coordinate left;
		public Coordinate leftStart;
		public Coordinate right;
		public Coordinate rightStart;

		public Route(Coordinate center, Coordinate speedVector, Coordinate front, Coordinate rear, Coordinate left, Coordinate right)
		{
			this.center = center;
			this.speedVector = speedVector;
			this.frontStart = front;
			this.rearStart = rear;
			this.leftStart = left;
			this.rightStart = right;
			this.front = front + speedVector;
			this.rear = rear + speedVector;
			this.left = left + speedVector;
			this.right = right + speedVector;
		}

		public RouteRib[] Ribs
		{
			get
			{
				if (ribs != null) return ribs;
				ribs = new[] { new RouteRib(leftStart, left), new RouteRib(rightStart, right), new RouteRib(frontStart, front),
				new RouteRib(rearStart, rear), new RouteRib(leftStart, rearStart), new RouteRib(rearStart, rightStart),
				new RouteRib(left, front), new RouteRib(front, right)};
				return ribs;
			}
		}
		private RouteRib[] ribs;

		public bool IsCrossingWith(Route other)
		{
			foreach(var rib in Ribs)
			{
				if(other.Ribs.Any(r => IsCrossing(rib.start, rib.finish, r.start, r.finish)))
				{
					return true;
				}
			}
			return false;
		}
	}

	public class RouteRib
	{
		public Coordinate start;
		public Coordinate finish;
		public RouteRib( Coordinate start, Coordinate finish)
		{
			this.start = start;
			this.finish = finish;
		}
	}
}
