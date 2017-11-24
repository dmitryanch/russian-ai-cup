using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using System.Linq;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
	public sealed class MyStrategy : IStrategy
	{
		public void Move(Player me, World world, Game game, Move move)
		{
			Me = me;
			if (world.TickIndex == 0)
			{
				FormArmy(world);
			}
			else
			{
				UpdateArmy(world);
			}
			GroupTask currentTask;
			if (me.RemainingActionCooldownTicks > 0 || (currentTask = Mine.GetTask(Opponent)) == null)
			{
				move.Action = ActionType.None;
				return;
			}
			ExecuteTask(currentTask, move);
			return;
		}

		void ExecuteTask(GroupTask task, Move move)
		{
			move.Action = task.action;
			if (task.group > 0) move.Group = task.group;
			switch (task.action)
			{
				case ActionType.Move:
					move.X = task.X;
					move.Y = task.Y;
					if (task.maxSpeed > 0) move.MaxSpeed = task.maxSpeed;
					break;
				case ActionType.ClearAndSelect:
					if (task.top > 0) move.Top = task.top;
					if (task.bottom > 0) move.Bottom = task.bottom;
					if (task.left > 0) move.Left = task.left;
					if (task.right > 0) move.Right = task.right;
					break;
				case ActionType.Scale:
					move.Factor = task.factor;
					move.X = task.X;
					move.Y = task.Y;
					break;
				case ActionType.Rotate:
					move.Angle = task.angle;
					move.X = task.X;
					move.Y = task.Y;
					break;
				case ActionType.TacticalNuclearStrike:
					move.VehicleId = task.vehicleId;
					move.X = task.X;
					move.Y = task.Y;
					break;
				case ActionType.SetupVehicleProduction:
					move.VehicleType = task.vehicleType;
					move.FacilityId = task.facilityId;
					break;
				default: break;
			}
			if (task.action != ActionType.None)
			{
				Mine.SetLastTask(task);
			}
		}

		#region Initialization and Updating
		public void FormArmy(World world)
		{
			Mine = new ArmyInfo { isMine = true, playerId = Me.Id };
			Opponent = new ArmyInfo { playerId = world.Players.First(p => p.Id != Me.Id).Id };
			Mine.Init(world);
			Opponent.Init(world);
		}

		public void UpdateArmy(World world)
		{
			Mine.Update(world);
			Opponent.Update(world);
		}
		#endregion

		#region Fields and Properties
		Player Me;
		ArmyInfo Mine;
		ArmyInfo Opponent;
		#endregion
	}
}