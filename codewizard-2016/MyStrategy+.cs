using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using static System.Math;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public sealed class MyStrategy : IStrategy
    {
        public void Move(Wizard self, World world, Game game, Move move)
        {
            //move.Speed = game.WizardForwardSpeed;
            //move.StrafeSpeed = game.WizardStrafeSpeed;
            //move.Turn = game.WizardMaxTurnAngle;
            //move.Action = ActionType.MagicMissile;

            if (!TryInitializeTick(self, world, game, move))
                return;

            InitializeStrategy();

            AnalyzeWorld();
            SetLearningStrategy();
            SetMovingStrategy();
            SetActionStrategy();
        }

        #region Initialize

        private bool TryInitializeTick(Wizard self, World world, Game game, Move move)
        {
            if (self == null || world == null || game == null || move == null)
                return false;
            _self = self;
            _world = world;
            _game = game;
            _move = move;

            if (_stuckTicks > 0) _stuckTicks--;
            return true;
        }

        private void InitializeStrategy()
        {
            if (KeyPointByFront != null && KeyPointByFront.Any()) return;
            var mapSize = _game.MapSize;
            KeyPointByFront = new Dictionary<LaneType, KeyPoint[]>();

            KeyPointByFront.Add(LaneType.Bottom, new KeyPoint[3]);
            KeyPointByFront[LaneType.Bottom][0] = new KeyPoint(0.1 * mapSize, 0.9 * mapSize);
            KeyPointByFront[LaneType.Bottom][1] = new KeyPoint(0.9 * mapSize, 0.9 * mapSize);
            KeyPointByFront[LaneType.Bottom][2] = new KeyPoint(0.9 * mapSize, 0.25 * mapSize);

            KeyPointByFront.Add(LaneType.Middle, new KeyPoint[3]);
            KeyPointByFront[LaneType.Middle][0] = new KeyPoint(0.1 * mapSize, 0.9 * mapSize);
            KeyPointByFront[LaneType.Middle][1] = new KeyPoint(0.5 * mapSize, 0.5 * mapSize);
            KeyPointByFront[LaneType.Middle][2] = new KeyPoint(0.75 * mapSize, 0.25 * mapSize);

            KeyPointByFront.Add(LaneType.Top, new KeyPoint[3]);
            KeyPointByFront[LaneType.Top][0] = new KeyPoint(0.1 * mapSize, 0.9 * mapSize);
            KeyPointByFront[LaneType.Top][1] = new KeyPoint(0.1 * mapSize, 0.1 * mapSize);
            KeyPointByFront[LaneType.Top][2] = new KeyPoint(0.75 * mapSize, 0.1 * mapSize);

            LeftBonusNextTime = 2500;
            RightBonusNextTime = 2500;
        }

        #endregion

        #region Analyze

        private void AnalyzeWorld()
        {
            AnalyzeTargets();
            AnalyzePosition();
            AnalyzeHealth();
            AnalyzeStatus();
            AnalyzeBaseState();
        }

        private void AnalyzeBaseState()
        {
            var baseBuilding =
                _world.Buildings.First(b => b.Type == BuildingType.FactionBase && b.Faction == _self.Faction);
            _baseAlert |= (PreviousBaseLife > baseBuilding.Life && baseBuilding.Life < 0.5 * baseBuilding.MaxLife);
            BaseLifeDelta = PreviousBaseLife - baseBuilding.Life;
            PreviousBaseLife = baseBuilding.Life;
        }

        private void AnalyzeHealth()
        {
            ForceMove |= _alert = _self.Life < 0.4 * _self.MaxLife;
            if (_world.TickIndex % 10 != 0) return;
            ForceMove |= _alert |= PreviousLife > _self.Life + 0.4 * _self.MaxLife;
            PreviousLife = _self.Life;
        }

        private void AnalyzeStatus()
        {
            if (_self.Statuses.Sum(s => (int)s.Type) > PreviousStatuses)
            {
                PreviousStatuses = _self.Statuses.Sum(s => (int)s.Type);
                PreviousWizardsStatuses = TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) + TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type));
                if (_self.X > 2500)
                {
                    RightBonusNextTime = _world.TickIndex + _game.BonusAppearanceIntervalTicks;
                    RightBonusCheckedAttempted = -1;
                }
                else
                {
                    LeftBonusNextTime = _world.TickIndex + _game.BonusAppearanceIntervalTicks;
                    LeftBonusCheckedAttempted = -1;
                }
            }
            else if (_self.Statuses.Sum(s => (int)s.Type) < PreviousStatuses)
            {
                PreviousStatuses = _self.Statuses.Sum(s => (int)s.Type);
                PreviousWizardsStatuses = TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) + TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type));
            }
            else if (TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) +
                     TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) > PreviousWizardsStatuses)
            {
                PreviousWizardsStatuses = TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) + TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type));
                var statusWizards =
                    (TeamWizards?.Where(w => w.Statuses != null && w.Statuses.Any()) ?? new Wizard[0]).Concat(
                        TargetWizards?.Where(w => w.Statuses != null && w.Statuses.Any()) ?? new Wizard[0]).ToArray();
                foreach (var wizard in statusWizards)
                {
                    foreach (
                        var status in
                            wizard.Statuses.Where(
                                status =>
                                    Abs(_world.TickIndex + status.RemainingDurationTicks + 100 - LeftBonusNextTime) >=
                                    10 &&
                                    Abs(_world.TickIndex + status.RemainingDurationTicks + 100 - RightBonusNextTime) >=
                                    10))
                    {
                        if (_world.TickIndex + status.RemainingDurationTicks + 100 > LeftBonusNextTime &&
                            wizard.GetDistanceTo(LeftBonusPoint.X, LeftBonusPoint.Y) < wizard.GetDistanceTo(RightBonusPoint.X, RightBonusPoint.Y))
                        {
                            LeftBonusNextTime = _world.TickIndex + status.RemainingDurationTicks + 100;
                            LeftBonusCheckedAttempted = -1;
                        }
                        else
                        {
                            RightBonusNextTime = _world.TickIndex + status.RemainingDurationTicks + 100;
                            RightBonusCheckedAttempted = -1;
                        }
                    }
                }
            }
            else if (TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) +
                     TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) < PreviousWizardsStatuses)
            {
                PreviousWizardsStatuses = TeamWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type)) + TargetWizards.Sum(w => w.Statuses.Sum(s => (int)s.Type));
            }
        }

        private void AnalyzePosition()
        {
            // todo siege?
            // todo stuck?
            if (_stuckTicks == 0 && Abs(_self.SpeedX) < 1 && Abs(_self.SpeedY) < 1 && (Abs(PreviousSpeed) > 0 || Abs(PreviousStrafeSpeed) > 0) && NearestUnits != null && NearestUnits.Any() || _stuckTicks > 0)
            {
                if (_needSetStuckAvoidingMovement && _stuckTicks == 0)
                {
                    _stuckAvoidingAttemt++;
                }
                else if (_stuckTicks == 0) _stuckAvoidingAttemt = 1;
                _stuckTicks = _stuckTicks > 0 ? _stuckTicks : 15;
            }
            _needSetStuckAvoidingMovement = _stuckTicks > 0;

            var bonusCheckingDistance = 10;//_self.VisionRange * 0.9
            if (_self.GetDistanceTo(LeftBonusPoint.X, LeftBonusPoint.Y) < bonusCheckingDistance && (TargetBonuses == null || !TargetBonuses.Any()))
            {
                LeftBonusCheckedAttempted = _world.TickIndex;
            }
            else if (_self.GetDistanceTo(RightBonusPoint.X, RightBonusPoint.Y) < bonusCheckingDistance && (TargetBonuses == null || !TargetBonuses.Any()))
            {
                RightBonusCheckedAttempted = _world.TickIndex;
            }
        }

        private void AnalyzeTargets()
        {
            TargetBonuses = _world.Bonuses;
            TeamMinions =
                _world.Minions?.Where(m => m.Faction == _self.Faction && m.GetDistanceTo(_self) <= _self.VisionRange)
                    .ToArray();
            TargetMinions =
                _world.Minions?.Where(
                    m =>
                        m.Faction != _self.Faction && m.Faction != Faction.Other && m.Faction != Faction.Neutral &&
                        m.GetDistanceTo(_self) <= _self.VisionRange)
                    .OrderBy(m => m.GetDistanceTo(_self)).ThenBy(m => m.Life)
                    .ToArray();
            TeamWizards =
                _world.Wizards?.Where(w => w.Faction == _self.Faction && w.GetDistanceTo(_self) <= _self.VisionRange)
                    .ToArray();
            TargetWizards =
                _world.Wizards?.Where(
                    w =>
                        w.Faction != _self.Faction && w.Faction != Faction.Other && w.Faction != Faction.Neutral &&
                        w.GetDistanceTo(_self) <= _self.VisionRange)
                    .OrderBy(m => m.GetDistanceTo(_self)).ThenBy(m => m.Life)
                    .ToArray();
            TeamBuildings =
                _world.Buildings?.Where(b => b.Faction == _self.Faction && b.GetDistanceTo(_self) <= _self.VisionRange)
                    .ToArray();
            TargetBuildings =
                _world.Buildings?.Where(
                    b =>
                        b.Faction != _self.Faction && b.Faction != Faction.Other && b.Faction != Faction.Neutral &&
                        b.GetDistanceTo(_self) <= _self.VisionRange)
                    .OrderBy(m => m.GetDistanceTo(_self)).ThenBy(m => m.Life)
                    .ToArray();
            AllTeam = (TeamMinions?.Cast<LivingUnit>() ?? new LivingUnit[0])
                .Concat(TeamBuildings?.Cast<LivingUnit>() ?? new LivingUnit[0])
                .Concat(TeamWizards?.Cast<LivingUnit>() ?? new LivingUnit[0]).ToArray();
            AllTargets = (TargetMinions?.Cast<LivingUnit>() ?? new LivingUnit[0])
                .Concat(TargetBuildings?.Cast<LivingUnit>() ?? new LivingUnit[0])
                .Concat(TargetWizards?.Cast<LivingUnit>() ?? new LivingUnit[0]).ToArray();
            NearestTargets = AllTargets.OrderBy(m => m.GetDistanceTo(_self)).ThenBy(m => m.Life).ToArray();
            TargetsToMagicAttack =
                AllTargets.Where(
                    t =>
                        _self.GetDistanceTo(t) <= _game.WizardCastRange &&
                        Abs(_self.GetAngleTo(t)) < _game.StaffSector / 2 + _game.WizardMaxTurnAngle)
                    .OrderBy(m => m.Life)
                    .ThenBy(m => m.GetDistanceTo(_self)).ToArray();
            TargetsToStaffAttack =
                AllTargets.Where(t => _self.GetDistanceTo(t) <= _game.StaffRange &&
                                      Abs(_self.GetAngleTo(t)) < _game.StaffSector / 2 + _game.WizardMaxTurnAngle)
                    .OrderBy(m => m.Life)
                    .ThenBy(m => m.GetDistanceTo(_self)).ToArray();
            PossibleTargets = AllTargets.Except(TargetsToMagicAttack)
                .Except(TargetsToStaffAttack)
                .OrderBy(m => Abs(_self.GetAngleTo(m)))
                .ThenBy(m => m.Life).ToArray();
            NearestUnits =
                AllTargets.Concat(AllTeam)
                    .Concat(_world.Trees ?? new LivingUnit[0])
                    .Concat(_world.Minions?.Where(m => m.Faction == Faction.Other || m.Faction == Faction.Neutral).ToArray() ?? new LivingUnit[0])
                    .Where(u => u.GetDistanceTo(_self) < u.Radius + _self.Radius + 10 && u.GetDistanceTo(_self) > 1)
                    .OrderBy(u => u.GetDistanceTo(_self))
                    .ToArray();


            var teamMinionsLife = TeamMinions?.Where(m => AllTargets.Any(t => t.GetDistanceTo(m) + t.Radius < m.VisionRange / 2)).Sum(m => m.Life) ?? 0;
            var teamMinionsDamage = TeamMinions?.Sum(m => m.Damage * AllTargets.Where(t => t.GetDistanceTo(m) + t.Radius < m.VisionRange / 2).ToArray().Length) ?? 0;
            var targetMinionsLife = TargetMinions?.Where(m => AllTeam.Any(t => t.GetDistanceTo(m) + t.Radius < m.VisionRange / 2)).Sum(m => m.Life) ?? 0;
            var targetMinionsDamage = TargetMinions?.Sum(m => m.Damage * AllTeam.Where(t => t.GetDistanceTo(m) + t.Radius < m.VisionRange / 2).ToArray().Length) ?? 0;
            var teamBuildingsLife = TeamBuildings?.Where(m => AllTargets.Any(t => t.GetDistanceTo(m) + t.Radius < m.AttackRange)).Sum(m => m.Life) ?? 0;
            var teamBuildingsDamage = TeamBuildings?.Sum(m => m.Damage * AllTargets.Where(t => t.GetDistanceTo(m) + t.Radius < m.AttackRange).ToArray().Length) ?? 0;
            var targetBuildingsLife = TargetBuildings?.Where(m => AllTeam.Any(t => t.GetDistanceTo(m) + t.Radius < m.AttackRange)).Sum(m => m.Life) ?? 0;
            var targetBuildingsDamage = TargetBuildings?.Sum(m => m.Damage * AllTeam.Where(t => t.GetDistanceTo(m) + t.Radius < m.AttackRange).ToArray().Length) ?? 0;
            var teamWizardsLife = TeamWizards?.Where(m => AllTargets.Any(t => t.GetDistanceTo(m) + t.Radius < m.CastRange)).Sum(w => w.Life) ?? _self.Life;
            var teamWizardsDamage = TeamWizards?.Sum(w => _game.StaffDamage * AllTargets.Where(t => t.GetDistanceTo(w) + t.Radius < w.CastRange).ToArray().Length) ?? _game.StaffDamage;
            var targetWizardsLife = TargetWizards?.Where(m => AllTeam.Any(t => t.GetDistanceTo(m) + t.Radius < m.CastRange)).Sum(w => w.Life) ?? 0;
            var targetWizardsDamage = TargetWizards?.Sum(w => _game.StaffDamage * AllTeam.Where(t => t.GetDistanceTo(w) + t.Radius < w.CastRange).ToArray().Length) ?? 0;

            var teamForce = teamBuildingsDamage + teamMinionsDamage + teamWizardsDamage;
            var targetForce = targetBuildingsDamage + targetMinionsDamage + targetWizardsDamage;
            var teamLife = teamMinionsLife + teamWizardsLife + teamBuildingsLife;
            var targetLife = targetMinionsLife + targetWizardsLife + targetBuildingsLife;
            var teamCount = (TeamMinions?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ??
                            0) + (TeamBuildings?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ??
                            0) + (TeamWizards?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ?? 0);
            var targetCount = (TargetMinions?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ??
                            0) + (TargetBuildings?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ??
                            0) + (TargetWizards?.Where(m => m.RemainingActionCooldownTicks > 0).ToArray().Length ?? 0);

            ForceMove = _stepBack = teamForce < targetForce && teamLife < targetLife && teamCount < targetCount;
            BraveHeart = teamForce > targetForce && teamLife > targetLife && teamCount > targetCount;
        }

        #endregion

        #region PlanStrategy

        private void SetStuckAvoidingMovement()
        {
            if (!_needSetStuckAvoidingMovement) return;
            if (NearestUnits.Length == 1)
            {
                MoveOut(NearestUnits.First(), withTurn: true);
                return;
            }
            if (NearestUnits.Length > 1)
            {
                var virtualAngle = NearestUnits.Sum(u => _self.GetAngleTo(u));
                var virtualNearest = new KeyPoint(
                    _self.X + _self.Radius / Tan(virtualAngle),
                    _self.Y + _self.Radius * Tan(virtualAngle));
                MoveOut(virtualNearest, withTurn: true);
                return;
            }
            MoveTo(CurrentRoute != null ? NextStep : NextKeyPoint, withTurn: true);
        }

        private void SetMovingStrategy()
        {
            if (Front == null) SetFront();
            SetNextKeyPoint();
            SetRoute();
            SetMovementAndAngle();
            SetStuckAvoidingMovement();
        }

        private void SetMovementAndAngle()
        {
            if (_stuckTicks > 0) return;
            if (HaveTarget)
            {
                var aim = NeedAttackDelay &&
                          TargetsToMagicAttack != null && TargetsToMagicAttack.Any()
                    ? TargetsToMagicAttack.First()
                    : TargetsToStaffAttack != null && TargetsToStaffAttack.Any()
                        ? TargetsToStaffAttack.First()
                        : PossibleTargets != null && PossibleTargets.Any()
                            ? PossibleTargets.First()
                            : TargetsToMagicAttack?.FirstOrDefault() ?? TargetsToStaffAttack.First();
                if (ForceMove || _alert || _stepBack)
                {
                    GoToNextKeyPoint(aim, true);
                }
                else
                {
                    SetPositionTo(aim);
                }
            }
            else
            {
                GoToNextKeyPoint(PossibleTargets?.FirstOrDefault(), ForceMove || _alert || _stepBack);
            }
        }

        private void SetRoute()
        {
            if (_world.TickIndex < 1000)
            {
                CurrentRoute = BuildRoute(_self, new KeyPoint(2000, 2400));
                return;
            }
            if (TryBuildRoutesForBonuses())
            {
                return;
            }
            if (_alert)
            {
                CurrentRoute = BuildRoute(_self, PreviousKeyPoint);
                return;
            }
            if (_stepBack)
            {
                var safePoint = FindSafePoint();
                CurrentRoute = BuildRoute(_self, safePoint);
                return;
            }
            //if (_baseAlert)
            //{
            //    var homeRoute = BuildRoute(_self, _basePoint);
            //    if (Abs(homeRoute.Duration * BaseLifeDelta / 10) < PreviousBaseLife - BaseLifeDelta)
            //    {
            //        CurrentRoute = homeRoute;
            //        return;
            //    }
            //}
            CurrentRoute = BuildRoute(_self, NextKeyPoint);
        }

        private void SetLearningStrategy()
        {
            // todo Learning
            if (_game.LevelUpXpValues == null || !_game.LevelUpXpValues.Any())
                return;
            if (_previousSkill > _self.Level)
            {
                _move.SkillToLearn = _learningPlan[_self.Level];
                _previousSkill = _self.Level;
            }
        }

        private void SetPositionTo(LivingUnit aim)
        {
            if (NearestTargets == null || !NearestTargets.Any())
                return;

            var nearestTarget = NearestTargets.First();
            if (nearestTarget.GetDistanceTo(_self) <= NearFightDistance)
            {
                MoveOut(nearestTarget);
            }
            else
            {
                MoveToAttack(aim);
            }
            TurnTo(aim);
        }

        private void SetNextKeyPoint()
        {
            NextKeyPoint = (_self.X + 200 > _self.Y
                    ? KeyPointByFront[Front ?? LaneType.Middle][2]
                    : KeyPointByFront[Front ?? LaneType.Middle][1]);
            PreviousKeyPoint = (_self.X - 200 > _self.Y
                    ? KeyPointByFront[Front ?? LaneType.Middle][1]
                    : KeyPointByFront[Front ?? LaneType.Middle][0]);
        }

        private void SetFront()
        {
            Front = LaneType.Middle;
        }

        private void SetActionStrategy()
        {
            if (!CanAct && !_needSetStuckAvoidingMovement)
                return;
            if (/*!HaveSteps || */!HaveTarget && _needSetStuckAvoidingMovement && (NearestTrees != null && NearestTrees.Any() ||
                OnWayTrees != null && OnWayTrees.Any()) && (NearestTrees?.Contains(NearestUnits.FirstOrDefault()) ?? false))
            {
                var aims = NearestTrees != null && NearestTrees.Any()
                    ? NearestTrees
                    : OnWayTrees != null && OnWayTrees.Any()
                        ? OnWayTrees
                        : NearestUnits.Where(m => m.Faction == Faction.Other || m.Faction == Faction.Neutral).ToArray();
                var aim = aims.FirstOrDefault(u => Abs(_self.GetAngleTo(u)) < _game.StaffSector / 2);
                if (aim != null)
                {
                    _move.CastAngle = _self.GetAngleTo(aim);
                    _move.MinCastDistance = _self.GetDistanceTo(aim) - aim.Radius - _self.Radius;
                    _move.Action = CanSpecialAct(ActionType.MagicMissile)
                        ? ActionType.MagicMissile
                        : ActionType.Staff;
                    return;
                }
                if (_stuckAvoidingAttemt > 0)
                {
                    aim = aims.FirstOrDefault();
                    if (aim != null)
                    {
                        _move.Turn = Min(Max(_self.GetAngleTo(aim), -_game.WizardMaxTurnAngle), _game.WizardMaxTurnAngle);
                    }
                }
            }
            //_move.Action = CanSpecialAct(ActionType.Shield)
            //    ? ActionType.Shield
            //    : CanSpecialAct(ActionType.Haste)
            //        ? ActionType.Haste
            //        : CanSpecialAct(ActionType.MagicMissile) &&
            //          TargetsToFireAttack != null && TargetsToFireAttack.Any()
            //            ? ActionType.Fireball
            //            : CanSpecialAct(ActionType.MagicMissile) &&
            //              TargetsToFrostAttack != null && TargetsToFrostAttack.Any()
            //                ? ActionType.FrostBolt
            //                : CanSpecialAct(ActionType.MagicMissile) &&
            //                  TargetsToMagicAttack != null && TargetsToMagicAttack.Any()
            //                    ? ActionType.MagicMissile
            //                    : CanSpecialAct(ActionType.MagicMissile) &&
            //                      (TargetsToStaffAttack != null && TargetsToStaffAttack.Any())
            //                        ? ActionType.Staff
            //                        : ActionType.None;
            _move.Action = CanSpecialAct(ActionType.MagicMissile) &&
                              TargetsToMagicAttack != null && TargetsToMagicAttack.Any()
                                ? ActionType.MagicMissile
                                : CanSpecialAct(ActionType.Staff) &&
                                  (TargetsToStaffAttack != null && TargetsToStaffAttack.Any())
                                    ? ActionType.Staff
                                    : ActionType.None;
        }

        #endregion

        #region Routing

        private KeyPoint FindSafePoint()
        {
            Func<KeyPoint, bool> where =
                p => GetLane(p) == GetLane(_self) &&
                TargetMinions.All(m => m.GetDistanceTo(p.X, p.Y) - _self.Radius - m.Radius > m.VisionRange / 2)
                     && TargetBuildings.All(b => b.GetDistanceTo(p.X, p.Y) - _self.Radius - b.Radius > b.AttackRange) &&
                     TargetWizards.All(w => w.GetDistanceTo(p.X, p.Y) - _self.Radius - w.Radius > w.CastRange);
            Func<KeyPoint, double> orderBy =
                //p => TargetMinions.Count(m => m.GetDistanceTo(p.X, p.Y) + m.Radius < _self.CastRange);
                p => p.GetDistanceTo(_basePoint);
            return FindNearestPointFrom(_self.X, _self.Y, where, orderBy);
        }

        private bool TryBuildRoutesForBonuses()
        {
            // todo Bonus
            var leftBonusPlanCheck = LeftBonusNextTime > _world.TickIndex;
            var rightBonusPlanCheck = RightBonusNextTime > _world.TickIndex;
            var result = (TargetBonuses != null && TargetBonuses.Any() &&
                          TryBuildCheckBonusRoute(new KeyPoint(TargetBonuses.OrderBy(b => b.GetDistanceTo(_self)).First()), anyway: true)) ||
                         (rightBonusPlanCheck && TryBuildCheckBonusRoute(RightBonusPoint, RightBonusNextTime)) ||
                         (leftBonusPlanCheck && TryBuildCheckBonusRoute(LeftBonusPoint, LeftBonusNextTime)) ||
                         !rightBonusPlanCheck && _self.GetDistanceTo(RightBonusPoint.X, RightBonusPoint.Y) < 1200 &&
                         Abs(RightBonusCheckedAttempted - _world.TickIndex) > 500 &&
                         TryBuildCheckBonusRoute(RightBonusPoint, anyway: true) || !leftBonusPlanCheck && _self.GetDistanceTo(LeftBonusPoint.X, LeftBonusPoint.Y) < 1200 &&
                         Abs(LeftBonusCheckedAttempted - _world.TickIndex) > 500 &&
                         TryBuildCheckBonusRoute(LeftBonusPoint, anyway: true);
            ForceMove |= result;
            return result;
        }

        private bool TryBuildCheckBonusRoute(KeyPoint point, int bonusTime = 0, bool anyway = false)
        {
            var route = BuildRoute(_self, point.X, point.Y);
            var wayDuration = route.Duration;
            if (anyway || (bonusTime - wayDuration - _world.TickIndex > 0 &&
                bonusTime - wayDuration - _world.TickIndex < 500))
            {
                CurrentRoute = route;
                return true;
            }
            return false;
        }

        private Route BuildRoute(CircularUnit unit, KeyPoint point)
        {
            return BuildRoute(unit.X, unit.Y, point.X, point.Y);
        }
        private Route BuildRoute(CircularUnit unit, double x, double y)
        {
            return BuildRoute(unit.X, unit.Y, x, y);
        }

        private Route BuildRoute(double x1, double y1, double x2, double y2)
        {
            var waypoints = new List<KeyPoint>() { new KeyPoint(x1, y1) };
            if (new KeyPoint(x1, y1).Equals(new KeyPoint(x2, y2)))
            {
                return new Route(waypoints.ToArray(), 0, 0);
            }
            if (GetLane(x1, y1) == Lane.Forest)
            {
                waypoints.Add(FindNearestPointFrom(x1, y1));
            }
            if ((x1 - (200 + _laneAddition) < y1 && x2 - (200 + _laneAddition) < y2 && x1 + (200 + _laneAddition) > y1 && x2 + (200 + _laneAddition) > y2) // bonus diagonal
                || (-x1 + 4000 - (200 + _laneAddition) < y1 && -x2 + 4000 - (200 + _laneAddition) < y2 && -x1 + 4000 + (200 + _laneAddition) > y1 && -x2 + 4000 + (200 + _laneAddition) > y2) // main diagonal
                || (x1 < 400 + _laneAddition && x2 < 400 + _laneAddition)   // left lane
                || (y1 < 400 + _laneAddition && y2 < 400 + _laneAddition)   // top lane
                || (x1 > 4000 - (400 + _laneAddition) && x2 > 4000 - (400 + _laneAddition))   // right lane
                || (y1 > 4000 - (400 + _laneAddition) && y2 > 4000 - (400 + _laneAddition))  // bottom lane
                || GetLane(x1, y1) == GetLane(x2, y2))
            {
                waypoints.Add(new KeyPoint(x2, y2));
            }
            else
            {
                try
                {
                    var intermediatePoints = IntermidientWayPointsByLane[GetLane(x1, y1)][GetLane(x2, y2)];
                    waypoints.AddRange(intermediatePoints);
                }
                catch
                { }
                waypoints.Add(new KeyPoint(x2, y2));
            }

            var totalDistance = 0d;
            for (var i = 1; i < waypoints.Count; i++)
            {
                totalDistance += waypoints[i - 1].GetDistanceTo(waypoints[i]);
            }
            return new Route(waypoints.ToArray(), totalDistance, totalDistance / _game.WizardForwardSpeed);
        }

        private KeyPoint[] CreateSteps()
        {
            //return CurrentRoute.Points.Length > 1 ? CurrentRoute.Points.Skip(1).ToArray() : CurrentRoute.Points;

            if (!IsUnitOnWay(SelfPoint, LastVisibleStepPoint, _self.Radius))
            {
                return new[] { LastVisibleStepPoint };
            }
            // One intermediate point
            var grid = CreateGrid();
            var stepsArray =
                OrderGridPoints(grid, SelfPoint, LastVisibleStepPoint).Select(
                    p => !IsUnitOnWay(SelfPoint, p, _self.Radius) && !IsUnitOnWay(p, LastVisibleStepPoint, _self.Radius) ? new[] { SelfPoint, p, LastVisibleStepPoint } : null).Where(s => s != null).ToArray();
            if (stepsArray.Any())
            {
                goto newBehavior;
            }

            // Two intermediate point
            //stepsArray =
            //    grid.Select(
            //        p =>
            //        {
            //            if (IsUnitOnWay(SelfPoint, p, _self.Radius)) return null;
            //            var inter2 =
            //                OrderGridPoints(grid, p, LastVisibleStepPoint).FirstOrDefault(
            //                    ip =>
            //                        !IsUnitOnWay(p, ip, _self.Radius) &&
            //                        !IsUnitOnWay(ip, LastVisibleStepPoint, _self.Radius));
            //            return !inter2.Equals(default(KeyPoint)) ? new[] { SelfPoint, p, inter2, LastVisibleStepPoint } : null;
            //        }).Where(s => s != null).ToArray();
            //if (stepsArray.Any())
            //{
            //    goto newBehavior;
            //}

            // Three intermediate point
            //stepsArray =
            //    grid.Select(
            //        p =>
            //        {
            //            if (IsUnitOnWay(SelfPoint, p, _self.Radius)) return null;
            //            var inter2 =
            //                grid.FirstOrDefault(
            //                    ip =>
            //                        !IsUnitOnWay(p, ip, _self.Radius) &&
            //                        !IsUnitOnWay(ip, LastVisibleStepPoint, _self.Radius));
            //            return !inter2.Equals(default(KeyPoint)) ? new KeyPoint[] { p, inter2 } : null;
            //        }).Where(s => s != null).ToArray();
            //if (stepsArray.Any())
            //{
            //    goto newBehavior;
            //}
            goto defaultBehavior;

            newBehavior:
            stepsArray = stepsArray.OrderBy(GetDistance).ToArray();
            return stepsArray.First().Skip(1).ToArray();

            defaultBehavior:
            return CurrentRoute.Points.Length > 1 ? CurrentRoute.Points.Skip(1).ToArray() : CurrentRoute.Points;
        }

        private KeyPoint[] OrderGridPoints(KeyPoint[] grid, KeyPoint start, KeyPoint finish)
        {
            return grid.OrderBy(p => start.GetDistanceTo(p) + p.GetDistanceTo(finish)).ToArray();
        }

        private KeyPoint[] CreateGrid()
        {
            const double gridStep = 15;
            const double sideAddition = 100;
            var nx = (int)Math.Round((Abs(SelfPoint.X - LastVisibleStepPoint.X) + 2 * sideAddition) / gridStep) + 1;
            var x = Enumerable.Range(0, nx).Select(i => Min(SelfPoint.X, LastVisibleStepPoint.X) - sideAddition + i * gridStep).ToArray();
            var ny = (int)Math.Round((Abs(SelfPoint.Y - LastVisibleStepPoint.Y) + 2 * sideAddition) / gridStep) + 1;
            var y = Enumerable.Range(0, ny).Select(i => Min(SelfPoint.Y, LastVisibleStepPoint.Y) - sideAddition + i * gridStep).ToArray();
            var grid = new List<KeyPoint>();
            for (var i = 0; i < nx; i++)
            {
                for (var j = 0; j < ny; j++)
                {
                    grid.Add(new KeyPoint(x[i], y[j]));
                }
            }
            return grid.ToArray();
        }

        private bool IsCleanSteps(List<KeyPoint> steps)
        {
            if (steps == null || steps.Count < 2)
            {
                return true;
            }
            var isClean = true;
            for (var i = 1; i < steps.Count; i++)
            {
                isClean &= !IsUnitOnWay(steps[i - 1], steps[i], _self.Radius);
            }
            return isClean;
        }

        private double GetDistance(KeyPoint[] steps)
        {
            if (steps == null || steps.Length < 2)
            {
                return 0d;
            }
            var distance = 0d;
            for (var i = 1; i < steps.Length; i++)
            {
                distance += steps[i].GetDistanceTo(steps[i - 1]);
            }
            return distance;
        }

        private bool IsUnitOnWay(KeyPoint startPoint, KeyPoint finishPoint, double radius)
        {
            //return AllButMe.Any(t => t.GetDistanceTo(startPoint.X, startPoint.Y) + t.GetDistanceTo(finishPoint.X, finishPoint.Y) <
            //       startPoint.GetDistanceTo(finishPoint) * 1.1);
            const double distanceToRouteCoefficient = 1;
            //return AllButMe.Any(u =>
            //{
            //    var norm = 1 / Sqrt(Pow(startPoint.Y - finishPoint.Y, 2) + Pow(startPoint.X - finishPoint.X, 2));
            //    var distanceToRoute = ((startPoint.Y - finishPoint.Y) * u.X + (finishPoint.X - startPoint.X) * u.Y +
            //                           (startPoint.X * finishPoint.Y - finishPoint.X * startPoint.Y)) * norm;
            //    return Abs(distanceToRoute) <= (radius + u.Radius) * distanceToRouteCoefficient;
            //});
            var res = AllButMe.Any(u =>
            {
                var norm = 1d / Sqrt(Pow(startPoint.Y - finishPoint.Y, 2) + Pow(startPoint.X - finishPoint.X, 2));
                var distanceToRoute = ((startPoint.Y - finishPoint.Y) * u.X + (finishPoint.X - startPoint.X) * u.Y +
                                       (startPoint.X * finishPoint.Y - finishPoint.X * startPoint.Y)) * norm;
                //return Abs(distanceToRoute) <= (radius + u.Radius) * 1d;
                var minX = Min(startPoint.X, finishPoint.X);
                var maxX = Max(startPoint.X, finishPoint.X);
                var minY = Min(startPoint.Y, finishPoint.Y);
                var maxY = Max(startPoint.Y, finishPoint.Y);
                var r = Abs(distanceToRoute) <= (radius + u.Radius) * 1d && (u.X > minX && u.X < maxX || u.Y > minY && u.Y < maxY);
                if (r)
                {
                    
                }
                return r;
            });
            if (!res)
            {

            }
            return res;
        }
        private KeyPoint FindNearestPointFrom(double x, double y, Func<KeyPoint, bool> wherePredicate = null, Func<KeyPoint, double> orderByPredicate = null)
        {
            const int step = 50;
            const int iterations = 20;
            var iteration = 0;
            var size = 15; // odd
            var initial = new KeyPoint(x, y);

            while (true)
            {
                var matrix = new KeyPoint[size][];
                matrix = new KeyPoint[size][];
                for (var i = 0; i < size; i++)
                {
                    matrix[i] = new KeyPoint[size];
                    for (var j = 0; j < size; j++)
                    {
                        matrix[i][j] = new KeyPoint(x - (size - 1) / 2 * step + i * step,
                            y - (size - 1) / 2 * step + j * step);
                    }
                }
                var nearest =
                    matrix.SelectMany(m => m)
                        .Where(p => !initial.Equals(p) && GetLane(p) != Lane.Forest && GetLane(p) != Lane.None && (wherePredicate == null || wherePredicate(p))).ToArray();
                if (nearest.Any())
                {
                    var orderedNearest = nearest.OrderBy(p => p.GetDistanceTo(x, y));
                    return orderByPredicate != null
                        ? orderedNearest.ThenBy(orderByPredicate).First()
                        : orderedNearest.First();
                }
                size += 10;
                if (++iteration > iterations) break;
            }
            return new KeyPoint(400, 3600);
        }

        private Lane GetLane(KeyPoint point)
        {
            return GetLane(point.X, point.Y);
        }
        private Lane GetLane(CircularUnit unit)
        {
            return GetLane(unit.X, unit.Y);
        }

        private Lane GetLane(double x, double y)
        {
            if (x < 400 + _laneAddition) return Lane.Left;
            if (y < 400 + _laneAddition) return Lane.Top;
            if (x > 4000 - (400 + _laneAddition)) return Lane.Right;
            if (y > 4000 - (400 + _laneAddition)) return Lane.Bottom;
            if (x - (200 + _laneAddition) < y && x + (200 + _laneAddition) > y) return x > 2000 ? Lane.BonusBottom : Lane.BonusTop;
            if (-x + 4000 - (200 + _laneAddition) < y && -x + 4000 + (200 + _laneAddition) > y) return x > 2000 ? Lane.MainTop : Lane.MainBottom;
            return Lane.Forest;
        }

        #endregion

        #region Movement

        private void MoveToAttack(LivingUnit aim)
        {
            var correction = 1 / _self.GetDistanceTo(aim) * NearFightDistance;
            var pointForAttack = new KeyPoint(aim.X + (_self.X - aim.X) * correction, aim.Y + (_self.Y - aim.Y) * correction);
            MoveTo(pointForAttack);
        }

        private void TurnTo(LivingUnit unit)
        {
            TurnTo(unit.X, unit.Y, unit.Radius);
        }

        private void TurnTo(KeyPoint point)
        {
            TurnTo(point.X, point.Y);
        }

        private void TurnTo(double x, double y, double radius = 0)
        {
            var angle = _self.GetAngleTo(x, y);
            _move.Turn = (angle > 0 ? 1 : -1) * Min(Abs(angle), _game.WizardMaxTurnAngle);
            if (radius > 0)
            {
                _move.CastAngle = angle;
                _move.MinCastDistance = _self.GetDistanceTo(x, y) - radius - _game.MagicMissileRadius;
            }
        }

        private void MoveOut(KeyPoint point, bool invert = true, bool withTurn = false)
        {
            MoveOut(point.X, point.Y, invert, withTurn);
        }

        private void MoveOut(CircularUnit unit, bool invert = true, bool withTurn = false)
        {
            MoveOut(unit.X, unit.Y, invert, withTurn);
        }

        private void MoveOut(double x, double y, bool invert = true, bool withTurn = false)
        {
            MoveTo(x, y, invert, withTurn);
        }

        private void GoToNextKeyPoint(CircularUnit unit, bool withTurn = false)
        {
            double x, y;
            if (unit == null &&
                (_self.GetDistanceTo(LeftBonusPoint.X, LeftBonusPoint.Y) < 50 && LeftBonusNextTime > _world.TickIndex &&
                 Abs(LeftBonusNextTime - _world.TickIndex) < 100 ||
                 _self.GetDistanceTo(RightBonusPoint.X, RightBonusPoint.Y) < 50 && RightBonusNextTime > _world.TickIndex &&
                 Abs(RightBonusNextTime - _world.TickIndex) < 100))
            {
                x = NextStep.X;
                y = NextStep.Y;
            }
            else
            {
                x = unit?.X ?? NextStep.X;
                y = unit?.Y ?? NextStep.Y;
            }
            TurnTo(x, y, unit?.Radius ?? 0);
            MoveTo(NextStep, withTurn: withTurn);
        }

        private void MoveTo(KeyPoint point, bool invert = false, bool withTurn = false)
        {
            MoveTo(point.X, point.Y, invert, withTurn);
        }

        private void MoveTo(CircularUnit unit)
        {
            MoveTo(unit.X, unit.Y);
        }

        private void MoveTo(double x, double y, bool invert = false, bool withTurn = true)
        {
            var angle = _self.GetAngleTo(x, y);
            var speed = Abs(angle) <= PI / 2 || invert ? 1 * _game.WizardForwardSpeed : -1 * _game.WizardBackwardSpeed;
            var strafe = (angle < 0 || invert ? -1 : 1) * Abs(Tan(angle)) * Abs(speed) * _game.WizardStrafeSpeed;
            if (withTurn)
                _move.Turn =
                    Min(
                        Max(
                            NormalizeAngle(invert ? angle + PI : angle), -_game.WizardMaxTurnAngle),
                        _game.WizardMaxTurnAngle);
            if (invert)
            {
                speed = withTurn ? _game.WizardForwardSpeed : -1 * speed;
                strafe = withTurn ? 0 : strafe;
            }
            var speedNorm = speed > 0 ? _game.WizardForwardSpeed : _game.WizardBackwardSpeed;
            var strafeNorm = _game.WizardStrafeSpeed;
            var normParam = Sqrt(Pow(speed / speedNorm, 2) + Pow(strafe / strafeNorm, 2));
            //_move.Speed = Min(Max(forceMoveAndTurn ? _game.WizardForwardSpeed : speed / normParam * speedNorm, -_game.WizardBackwardSpeed), _game.WizardForwardSpeed);
            //_move.StrafeSpeed = Min(Max(forceMoveAndTurn ? 0 : strafe / normParam * strafeNorm, -_game.WizardStrafeSpeed), _game.WizardStrafeSpeed);
            _move.Speed = Min(Max(speed / normParam * speedNorm, -_game.WizardBackwardSpeed), _game.WizardForwardSpeed);
            _move.StrafeSpeed = Min(Max(strafe / normParam * strafeNorm, -_game.WizardStrafeSpeed), _game.WizardStrafeSpeed);

            PreviousSpeed = speed;
            PreviousStrafeSpeed = strafe;
            PreviousPosition = new KeyPoint(_self.X, _self.Y);
        }

        private double NormalizeAngle(double angle)
        {
            return angle > PI ? angle - 2 * PI : angle < -PI ? angle + 2 * PI : angle;
        }

        private bool CanStuck(CircularUnit first, double speedX1, double speedY1, CircularUnit second, double speedX2, double speedY2)
        {
            return CanStuck(first.X, speedX1, first.Y, speedY1, first.Radius, second.X, speedX2, second.Y, speedY2, second.Radius);
        }

        private bool CanStuck(CircularUnit first, CircularUnit second)
        {
            return CanStuck(first.X, first.SpeedX, first.Y, first.SpeedY, first.Radius, second.X, second.SpeedX, second.Y, second.SpeedY, second.Radius);
        }

        private bool CanStuck(double x1, double speedX1, double y1, double speedY1, double radius1, double x2, double speedX2, double y2, double speedY2, double radius2)
        {
            var range = Enumerable.Range(1, 10).ToArray();
            return
                range.Any(
                    m =>
                        Sqrt(Pow(Abs(x1 + speedX1 * m - x2 - speedX2 * m), 2) +
                             Pow(Abs(y1 + speedY1 * m - y2 - speedY2 * m), 2)) <=
                        radius1 + radius2);
        }

        #endregion

        #region Properties

        //Map
        private KeyPoint PreviousPosition { get; set; }
        private Dictionary<LaneType, KeyPoint[]> KeyPointByFront { get; set; }
        private KeyPoint SelfPoint => new KeyPoint(_self.X, _self.Y);
        private LaneType? Front { get; set; }
        private int CurrentPosition => CurrentRoute.Points.ToList().IndexOf(CurrentRoute.Points.OrderBy(p => _self.GetDistanceTo(p.X, p.Y)).First());
        private readonly KeyPoint LeftBonusPoint = new KeyPoint(1200, 1200);
        private readonly KeyPoint RightBonusPoint = new KeyPoint(2800, 2800);

        // Routing
        private KeyPoint NextKeyPoint { get; set; }
        private KeyPoint PreviousKeyPoint { get; set; }
        private KeyPoint NextRoutePoint
            => CurrentRoute.Points.Length > 1 ? CurrentRoute.Points.First(p => !p.Equals(SelfPoint)) : CurrentRoute.Points[0];
        private KeyPoint NextStep => NextSteps.First();
        private double DistanceToNextRoutePoint => NextRoutePoint.GetDistanceTo(_self.X, _self.Y);
        private KeyPoint LastVisibleStepPoint => DistanceToNextRoutePoint > _self.VisionRange
                ? new KeyPoint(_self.X + (NextRoutePoint.X - _self.X) * _self.VisionRange / DistanceToNextRoutePoint,
                    _self.Y + (NextRoutePoint.Y - _self.Y) * _self.VisionRange / DistanceToNextRoutePoint)
                : NextRoutePoint;
        private KeyPoint[] NextSteps { get; set; }
        private bool HaveSteps => NextSteps != null && NextSteps.Any();
        private Route _сurrentRoute;
        private Route CurrentRoute
        {
            get { return _сurrentRoute; }
            set
            {
                _сurrentRoute = value;
                NextSteps = CreateSteps();
            }
        }
        // Fighting
        private double NearFightDistance => BraveHeart && !_alert ? _game.StaffRange : _self.CastRange * 0.9;
        //private double NearFightDistance =>  _self.CastRange * 0.9;
        private bool CanAct => _self.RemainingActionCooldownTicks == 0;
        private bool CanSpecialAct(ActionType action)
            =>
                _self.RemainingCooldownTicksByAction[(int)action] == 0 &&
                (action == ActionType.FrostBolt && _self.Mana > _game.FrostBoltManacost ||
                 action == ActionType.Fireball && _self.Mana > _game.FireballManacost
                 || action == ActionType.Shield && _self.Mana > _game.ShieldManacost
                 || action == ActionType.Haste && _self.Mana > _game.HasteManacost
                 || action == ActionType.Staff || action == ActionType.MagicMissile);

        private bool NeedAttackDelay => (_self.RemainingCooldownTicksByAction[(int)ActionType.MagicMissile] -
                                         _self.RemainingActionCooldownTicks) < MaxAttackingDelay ||
                                        (_self.RemainingCooldownTicksByAction[(int)ActionType.Fireball] -
                                         _self.RemainingActionCooldownTicks) < MaxAttackingDelay ||
                                        (_self.RemainingCooldownTicksByAction[(int)ActionType.FrostBolt] -
                                         _self.RemainingActionCooldownTicks) < MaxAttackingDelay;
        public bool HaveTarget => TargetsToMagicAttack != null && TargetsToMagicAttack.Any() ||
                                  TargetsToStaffAttack != null && TargetsToStaffAttack.Any();
        private bool ForceMove { get; set; }
        private bool BraveHeart { get; set; }

        // Units
        private Bonus[] TargetBonuses { get; set; }
        private Building[] TargetBuildings { get; set; }
        private Building[] TeamBuildings { get; set; }
        private Wizard[] TargetWizards { get; set; }
        private Wizard[] TeamWizards { get; set; }
        private Minion[] TargetMinions { get; set; }
        private Minion[] TeamMinions { get; set; }
        private LivingUnit[] TargetsToFireAttack { get; set; }
        private LivingUnit[] TargetsToFrostAttack { get; set; }
        private LivingUnit[] TargetsToMagicAttack { get; set; }
        private LivingUnit[] TargetsToStaffAttack { get; set; }
        private LivingUnit[] PossibleTargets { get; set; }
        private LivingUnit[] NearestTargets { get; set; }
        private LivingUnit[] NearestUnits { get; set; }
        private Tree[] NearestTrees => _world.Trees?.Where(
                    t => t.GetDistanceTo(_self) < t.Radius + _self.Radius + _game.StaffRange)
                    .OrderBy(u => Abs(_self.GetAngleTo(u)))
                    .ToArray();
        private Tree[] OnWayTrees => _world.Trees?.Where(
                    t =>
                        t.GetDistanceTo(_self) < t.Radius + _self.Radius + _game.WizardCastRange && IsUnitOnWay(SelfPoint, NextStep, _self.Radius))
                    .OrderBy(u => Abs(_self.GetAngleTo(u)))
                    .ToArray();
        private LivingUnit[] AllTargets { get; set; }
        private LivingUnit[] AllTeam { get; set; }

        private LivingUnit[] AllUnits
            =>
                AllTeam.Concat(AllTargets)
                    .Concat(_world.Trees)
                    .Concat(_world.Minions.Where(w => w.Faction != Faction.Other && w.Faction != Faction.Neutral))
                    .ToArray();

        private LivingUnit[] AllButMe => AllUnits.Except(new[] { _self }).ToArray();



        // History & Timing
        private double PreviousSpeed { get; set; }
        private double PreviousStrafeSpeed { get; set; }
        private int PreviousLife { get; set; }
        private int PreviousBaseLife { get; set; }
        private int BaseLifeDelta { get; set; }
        private int PreviousStatuses { get; set; }
        private int PreviousWizardsStatuses { get; set; }
        private int LeftBonusNextTime { get; set; }
        private int RightBonusNextTime { get; set; }
        private int RightBonusCheckedAttempted { get; set; }
        private int LeftBonusCheckedAttempted { get; set; }

        #endregion

        #region Fields and Constants

        private Wizard _self;
        private World _world;
        private Game _game;
        private Move _move;

        private bool _alert;
        private bool _baseAlert;
        private bool _stepBack;
        private bool _needSetStuckAvoidingMovement;
        private int _stuckAvoidingAttemt;
        private int _stuckTicks;
        private const int MaxAttackingDelay = 5;
        private int _previousSkill = 0;
        private const int _laneAddition = 300;
        private readonly KeyPoint _basePoint = new KeyPoint(400, 3600);

        private readonly SkillType[] _learningPlan = new[]
        {
            SkillType.StaffDamageBonusPassive1,
            SkillType.StaffDamageBonusAura1,
            SkillType.StaffDamageBonusPassive2,
            SkillType.StaffDamageBonusAura2,
            SkillType.Fireball,
            SkillType.MagicalDamageBonusPassive1,
            SkillType.MagicalDamageBonusAura1,
            SkillType.MagicalDamageBonusPassive2,
            SkillType.MagicalDamageBonusAura2,
            SkillType.FrostBolt,
            SkillType.RangeBonusPassive1,
            SkillType.RangeBonusAura1,
            SkillType.RangeBonusPassive2,
            SkillType.RangeBonusAura2,
            SkillType.AdvancedMagicMissile,
            SkillType.MovementBonusFactorPassive1,
            SkillType.MovementBonusFactorAura1,
            SkillType.MovementBonusFactorPassive2,
            SkillType.MovementBonusFactorAura2,
            SkillType.Haste,
            SkillType.MagicalDamageAbsorptionPassive1,
            SkillType.MagicalDamageAbsorptionAura1,
            SkillType.MagicalDamageAbsorptionPassive2,
            SkillType.MagicalDamageAbsorptionAura2,
            SkillType.Shield
        };

        private readonly Dictionary<Lane, Dictionary<Lane, KeyPoint[]>> IntermidientWayPointsByLane =
            new Dictionary<Lane, Dictionary<Lane, KeyPoint[]>>
            {
                { Lane.Left, new Dictionary<Lane, KeyPoint[]> { {Lane.Top, new [] {new KeyPoint(400, 800), new KeyPoint(800, 400), }}, { Lane.Bottom, new[] { new KeyPoint(400, 3200), new KeyPoint(800, 3600), } }, { Lane.BonusTop, new[] { new KeyPoint(400, 800) } }, { Lane.MainBottom, new[] { new KeyPoint(400, 3200) } } } },
                { Lane.Top, new Dictionary<Lane, KeyPoint[]> { {Lane.Left, new [] {new KeyPoint(800, 400), new KeyPoint(400, 800), }} ,{Lane.Right, new [] {new KeyPoint(3200, 400), new KeyPoint(3600, 800), }}, { Lane.BonusTop, new[] { new KeyPoint(800, 400) } }, { Lane.MainTop, new[] { new KeyPoint(3200, 400) } } } },
                { Lane.Right, new Dictionary<Lane, KeyPoint[]> { {Lane.Top, new [] {new KeyPoint(3600, 800), new KeyPoint(3200, 400), }} ,{Lane.Bottom, new [] {new KeyPoint(3600, 3200), new KeyPoint(3200, 3600), }}, { Lane.BonusBottom, new[] { new KeyPoint(3600, 3200) } }, { Lane.MainTop, new[] { new KeyPoint(3600, 800) } } } },
                { Lane.Bottom, new Dictionary<Lane, KeyPoint[]> { {Lane.Left, new [] {new KeyPoint(800, 3600), new KeyPoint(400, 3200), }}, { Lane.Right, new[] { new KeyPoint(3200, 3600), new KeyPoint(3600, 3200), } }, { Lane.BonusBottom, new[] { new KeyPoint(3200, 3600) } }, { Lane.MainBottom, new[] { new KeyPoint(800, 3600) } } } },
                { Lane.MainTop, new Dictionary<Lane, KeyPoint[]> { {Lane.BonusBottom, new [] {new KeyPoint(2400, 2000), }}, { Lane.BonusTop, new[] { new KeyPoint(2000, 1600) } } } },
                { Lane.MainBottom, new Dictionary<Lane, KeyPoint[]> { {Lane.BonusBottom, new [] {new KeyPoint(2000, 2400), }}, { Lane.BonusTop, new[] { new KeyPoint(1600, 2000) } }, { Lane.Left, new[] { new KeyPoint(400, 3200) } } } },
                { Lane.BonusTop, new Dictionary<Lane, KeyPoint[]> { {Lane.MainBottom, new [] {new KeyPoint(1600, 2000), }}, { Lane.MainTop, new[] { new KeyPoint(2000, 1600) } }, { Lane.Left, new[] { new KeyPoint(1600, 2000), new KeyPoint(400, 3200) } },{ Lane.Top, new[] { new KeyPoint(800, 400) } } } },
                { Lane.BonusBottom, new Dictionary<Lane, KeyPoint[]> { {Lane.MainBottom, new [] {new KeyPoint(2000, 2400), }}, { Lane.MainTop, new[] { new KeyPoint(2400, 2000) } }, { Lane.Right, new[] { new KeyPoint(3600, 3200) } }, { Lane.Left, new[] { new KeyPoint(2000, 2400), new KeyPoint(400, 3200) } }, { Lane.Top, new[] { new KeyPoint(2400, 2000) } } } },
            };

        #endregion

        #region Nested

        public struct KeyPoint : IEquatable<KeyPoint>
        {
            private readonly double _x;
            public double X => _x;
            private readonly double _y;
            public double Y => _y;

            public KeyPoint(double x, double y)
            {
                _x = x;
                _y = y;
            }

            public KeyPoint(CircularUnit unit) : this(unit.X, unit.Y)
            { }

            public bool Equals(KeyPoint other)
            {
                return Abs(X - other.X) < 1e-1 && Abs(Y - other.Y) < 1e-1;
            }

            public override bool Equals(object obj)
            {
                return obj is KeyPoint && this.Equals((KeyPoint)obj);
            }

            public override int GetHashCode()
            {
                return X.GetHashCode() * 13 + Y.GetHashCode();
            }

            public double GetDistanceTo(double x, double y)
            {
                return Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
            }

            public double GetDistanceTo(KeyPoint keyPoint)
            {
                return GetDistanceTo(keyPoint.X, keyPoint.Y);
            }

            public override string ToString()
            {
                return $"<X: {X}, Y: {Y}>";
            }
        }

        public class Route
        {
            public double Duration { get; }
            public double Distance { get; }
            public KeyPoint[] Points { get; }

            public Route(KeyPoint[] waypoints, double distance, double duration)
            {
                Duration = duration;
                Distance = distance;
                Points = waypoints;
            }
            public Route(KeyPoint[] waypoints)
            {
                var totalDistance = 0d;
                for (var i = 1; i < waypoints.Length; i++)
                {
                    totalDistance += waypoints[i - 1].GetDistanceTo(waypoints[i]);
                }
                Distance = totalDistance;
                Points = waypoints;
            }
        }
        public enum Lane
        {
            None,
            Left,
            Top,
            Right,
            Bottom,
            MainTop,
            MainBottom,
            BonusTop,
            BonusBottom,
            Forest
        }

        #endregion
    }
}