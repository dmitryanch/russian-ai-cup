using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using System;
using static Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Geometry;

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

		public bool IsCrossingWith(Route other)
		{
			return IsCrossing(leftStart, left, other.leftStart, other.left)
				|| IsCrossing(leftStart, left, other.rightStart, other.right)
				|| IsCrossing(leftStart, left, other.frontStart, other.front)
				|| IsCrossing(leftStart, left, other.rearStart, other.rear)

				|| IsCrossing(rightStart, right, other.leftStart, other.left)
				|| IsCrossing(rightStart, right, other.rightStart, other.right)
				|| IsCrossing(rightStart, right, other.frontStart, other.front)
				|| IsCrossing(rightStart, right, other.rearStart, other.rear)

				|| IsCrossing(frontStart, front, other.leftStart, other.left)
				|| IsCrossing(frontStart, front, other.rightStart, other.right)
				|| IsCrossing(frontStart, front, other.frontStart, other.front)
				|| IsCrossing(frontStart, front, other.rearStart, other.rear)

				|| IsCrossing(rearStart, rear, other.leftStart, other.left)
				|| IsCrossing(rearStart, rear, other.rightStart, other.right)
				|| IsCrossing(rearStart, rear, other.frontStart, other.front)
				|| IsCrossing(rearStart, rear, other.rearStart, other.rear);
		}
	}
}
