using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using System;
using System.Collections.Generic;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{

	public class Coordinate
	{
		public double X;
		public double Y;
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
		public double angle;
		public double front;
		public double rear;
		public double left;
		public double right;

		public Route(Dictionary<long, Vehicle> vehicles, double sX, double sY, double dX, double dY)
		{

		}
	}
}
