using System;
using static System.Math;
namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
	public class Geometry
	{
		public static double GetAngle(double x1, double y1)
		{
			return GetAngle(0, 0, x1, y1);
		}

		public static double GetAngle(double x1, double y1, double x2, double y2)
		{
			var angle = Atan((y2 - y1) / (x2 - x1));
			return angle > 0
				? (y2 - y1) > 0
					? angle
					: angle - PI
				: (y2 - y1) < 0
					? angle
					: PI + angle;
		}

		public static bool IsCrossing(double p1X, double p1Y, double p2X, double p2Y, double p3X, double p3Y, double p4X, double p4Y)
		{
			return IsCrossing(new Coordinate { X = p1X, Y = p1Y }, new Coordinate { X = p2X, Y = p2Y }, 
				new Coordinate { X = p3X, Y = p3Y }, new Coordinate { X = p4X, Y = p4Y });
		}

		//метод, проверяющий пересекаются ли 2 отрезка [p1, p2] и [p3, p4]
		public static bool IsCrossing(Coordinate p1, Coordinate p2, Coordinate p3, Coordinate p4)
		{
			//сначала расставим точки по порядку, т.е. чтобы было p1.X <= p2.X
			if (p2.X < p1.X)
			{
				Coordinate tmp = p1;
				p1 = p2;
				p2 = tmp;
			}

			//и p3.X <= p4.X

			if (p4.X < p3.X)
			{

				var tmp = p3;

				p3 = p4;

				p4 = tmp;

			}

			//проверим существование потенциального интервала для точки пересечения отрезков

			if (p2.X < p3.X)
			{

				return false; //ибо у отрезков нету взаимной абсциссы

			}

			//если оба отрезка вертикальные

			if ((p1.X - p2.X == 0) && (p3.X - p4.X == 0))
			{

				//если они лежат на одном X

				if (p1.X == p3.X)
				{

					//проверим пересекаются ли они, т.е. есть ли у них общий Y

					//для этого возьмём отрицание от случая, когда они НЕ пересекаются

					if (!((Math.Max(p1.Y, p2.Y) < Math.Min(p3.Y, p4.Y)) ||

					(Math.Min(p1.Y, p2.Y) > Math.Max(p3.Y, p4.Y))))
					{

						return true;

					}

				}

				return false;

			}

			double Xa, A1, b1, A2, b2;
			//найдём коэффициенты уравнений, содержащих отрезки

			//f1(x) = A1*x + b1 = y

			//f2(x) = A2*x + b2 = y

			//если первый отрезок вертикальный

			if (p1.X - p2.X == 0)
			{

				//найдём Xa, Ya - точки пересечения двух прямых

				Xa = p1.X;

				A2 = (p3.Y - p4.Y) / (p3.X - p4.X);

				b2 = p3.Y - A2 * p3.X;

				double Ya = A2 * Xa + b2;

				if (p3.X <= Xa && p4.X >= Xa && Math.Min(p1.Y, p2.Y) <= Ya &&

				Math.Max(p1.Y, p2.Y) >= Ya)
				{

					return true;

				}

				return false;

			}

			//если второй отрезок вертикальный

			if (p3.X - p4.X == 0)
			{

				//найдём Xa, Ya - точки пересечения двух прямых

				Xa = p3.X;

				A1 = (p1.Y - p2.Y) / (p1.X - p2.X);

				b1 = p1.Y - A1 * p1.X;

				double Ya = A1 * Xa + b1;

				if (p1.X <= Xa && p2.X >= Xa && Math.Min(p3.Y, p4.Y) <= Ya &&

				Math.Max(p3.Y, p4.Y) >= Ya)
				{

					return true;

				}

				return false;

			}

			//оба отрезка невертикальные

			A1 = (p1.Y - p2.Y) / (p1.X - p2.X);

			A2 = (p3.Y - p4.Y) / (p3.X - p4.X);

			b1 = p1.Y - A1 * p1.X;

			b2 = p3.Y - A2 * p3.X;

			if (A1 == A2)
			{

				return false; //отрезки параллельны

			}

			//Xa - абсцисса точки пересечения двух прямых

			Xa = (b2 - b1) / (A1 - A2);

			if ((Xa < Math.Max(p1.X, p3.X)) || (Xa > Math.Min(p2.X, p4.X)))
			{

				return false; //точка Xa находится вне пересечения проекций отрезков на ось X

			}

			else
			{

				return true;

			}

		}
	}
}
