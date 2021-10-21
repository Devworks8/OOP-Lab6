﻿/* Lab Question (Test 2) 
 * 
 * Here we have used a List to hold the phonons. Given that we will need to remove phonons
 * from many different locations of the List (front, middle, back) do you think
 * this is an appropriate data structure to use? Keep in mind, we will also need repeatedly
 * iterate over the List and the List could contain many phonons. Random access is not required.
 * Justify your choice of a different data structure or explain why a List is a good choice.
 * 
 * Can you think of a clever way to remove an element from the middle of a List without having
 * to shift the memory contents of the List? 
 */
 
using System;
using System.Collections.Generic;

using Psim.Geometry2D;
using Psim.Particles;
using Psim.Materials;

namespace Psim.ModelComponents
{
	public enum SurfaceLocation
	{
		left = 0,
		top = 1,
		right = 2,
		bot = 3
	}

	public class Cell : Rectangle
	{ 
		private const int NUM_SURFACES = 4;
		private List<Phonon> phonons = new List<Phonon>() { };
		private List<Phonon> incomingPhonons = new List<Phonon>() { };
		private ISurface[] surfaces = new ISurface[NUM_SURFACES];
		public List<Phonon> Phonons { get { return phonons; } }
		private Sensor sensor;

		public Cell(double length, double width, Sensor sensor) : base(length, width)
		{
			this.sensor = sensor;
			this.sensor.AddToArea(this.Area); // Each time a cell is linked to a sensor, the area that the sensor covers must increase
			for (int i = 0; i < NUM_SURFACES; ++i)
			{
				surfaces[i] = new BoundarySurface((SurfaceLocation)i, this);
			}
		}
		
		public void SetEmitSurface(SurfaceLocation location, double temp)
		{
			// Thanks Josh
			surfaces[(int)location] = new EmitSurface(location, this, temp); 
		}

		public void SetTransitionSurface(SurfaceLocation location, Cell cell)
		{
			surfaces[(int)location] = new TransitionSurface(location, cell);
		}
		
		/// <summary>
		/// Finds the total amount of emission energy generated over the course of the simulation
		/// from this cell. Returns 0 if the cell has no emitting surfaces
		/// Hint: Call the appropriate method on all emitting surfaces in the cell
		/// </summary>
		/// <param name="tEq">The equilibrium temperature of the system</param>
		/// <param name="simTime">The total simulation time</param>
		/// <returns>The total amount of emitting energy generated by this cell</returns>
		public double EmitEnergy(double tEq, double simTime)
		{
			// Thanks Andrew
			double totalEmitEnergy = 0;
			foreach (ISurface surface in surfaces)
			{
				if (surface is EmitSurface emitSurface)
				{
					totalEmitEnergy += emitSurface.GetEmitEnergy(tEq, simTime, Width);
				}
			}
			return totalEmitEnergy;
		}

		/// <summary>
		/// Sets the number of phonons each emitting surface will emit 
		/// at each time step of the simulation
		/// Hint: call the appropriate methods on all the emitting surfaces in the cell
		/// </summary>
		/// <param name="tEq">The equilibrium temperature of the system</param>
		/// <param name="effEnergy">The effective energy of a phonon packet</param>
		/// <param name="timeStep">The simulation time step (discrete time interval)</param>
		public void SetEmitPhonons(double tEq, double effEnergy, double timeStep)
		{
			foreach (ISurface surface in surfaces)
			{
				if (surface is EmitSurface emitSurface)
				{
					var loc = emitSurface.Location;
					if (loc == SurfaceLocation.left || loc == SurfaceLocation.right)
						emitSurface.SetEmitPhonons(tEq, effEnergy, timeStep, Width);
					else
						emitSurface.SetEmitPhonons(tEq, effEnergy, timeStep, Length);
				}
			}
		}

		/// <summary>
		/// Returns the data required to calibrate emitting surfaces based on the cell's material
		/// </summary>
		/// <param name="temp">The temperature of an emitting surface</param>
		/// <param name="emitEnergy">The baseline amount of energy generated by an emitting surface</param>
		/// <returns>The emit table cumulative distribution</returns>		
		public Tuple<double, double>[] EmitData(double temp, out double emitEnergy)
		{
			return sensor.GetEmitData(temp, out emitEnergy);
		}

		/// <summary>
		/// Gets the initial energy of a cell. Will return 0 if InitTemp = tEq.
		/// </summary>
		/// <param name="tEq">The equilibrium temperature of the system</param>
		/// <returns>The initial energy contained in a cell</returns>
		public double InitEnergy(double tEq)
		{
			return Area * sensor.HeatCapacity * Math.Abs(sensor.InitTemp - tEq);
		}

		/// <summary>
		/// Adds a phonon to the main phonon 'array' of the cell.
		/// </summary>
		/// <param name="p">The phonon that will be added</param>
		public void AddPhonon(Phonon p)
		{
			phonons.Add(p);
		}

		/// <summary>
		/// Adds a phonon to the incoming phonon 'array' of the cell
		/// The incoming phonon will come from the phonons 'array' of another cell
		/// </summary>
		/// <param name="p">The phonon that will be added</param>
		public void AddIncPhonon(Phonon p)
		{
			// This could cause a very nasty bug later on - be sure to verify now
			incomingPhonons.Add(p);
		}

		/// <summary>
		/// Merges the incoming phonons with the existing phonons and clears the incoming phonons
		/// </summary>
		public void MergeIncPhonons()
		{
			phonons.AddRange(incomingPhonons);
			incomingPhonons.Clear();
		}

		/// <summary>
		/// Returns the surface at SurfaceLocation loc
		/// </summary>
		/// <param name="loc">The SurfaceLocation of the surface to be returned</param>
		/// <returns>The surface at location loc</returns>
		public ISurface GetSurface(SurfaceLocation loc)
		{
			return surfaces[(int)loc];
		}

		/// <summary>
		/// Moves a phonon to the surface that it will impact first.
		/// The phonon will be moved to the surface and the surface!!!!!
		/// it impacts is returned
		/// </summary>
		/// <param name="p">The phonon to be moved</param>
		/// <returns>The surface that the phonon collides with or null if it doesnt impact surface</returns>
		public SurfaceLocation? MoveToNearestSurface(Phonon p)
		{
			// Returns the time taken to for a phonon to move back into the cell or 0 if the phonon did not exit the cell
			double GetTime(double dist, double pos, double vel)
			{
				if (pos <= 0) { return pos / vel; } // pos is negative therefore vel must be negative
				else if (pos >= dist) { return (pos - dist) / vel; } // pos is + therefore vel is + and len < pos
				else return 0; // No surface was reached
			}

			p.Drift(p.DriftTime);
			p.GetCoords(out double px, out double py);
			p.GetDirection(out double dx, out double dy);
			double vx = dx * p.Speed;
			double vy = dy * p.Speed;

			// The longer the time, the sooner the phonon impacted the corresponding surface
			double timeToSurfaceX = (vx != 0) ? GetTime(Length, px, vx) : 0;
			double timeToSurfaceY = (vy != 0) ? GetTime(Width, py, vy) : 0;

			// Time needed to backtrack the phonon to the first surface collision
			double backtrackTime = Math.Max(timeToSurfaceX, timeToSurfaceY);
			p.DriftTime = backtrackTime;
			if (backtrackTime == 0) { return null; } // The phonon did not collide with a surface
			p.Drift(-backtrackTime);

			// Miminize FP errors and determine impacted surface
			if (backtrackTime == timeToSurfaceX)
			{
				if (vx < 0)
				{
					p.SetCoords(0, null);
					return SurfaceLocation.left;
				}
				else
					p.SetCoords(Length, null);
				return SurfaceLocation.right;
			}
			else
			{
				if (vy < 0)
				{
					p.SetCoords(null, 0);
					return SurfaceLocation.bot;
				}
				else
				{
					p.SetCoords(null, Width);
					return SurfaceLocation.top;
				}
			}
		}

		public void TakeMeasurements(double effEnergy, double tEq)
		{
			sensor.TakeMeasurements(phonons, effEnergy, tEq);
		}

		public override string ToString()
		{
			return string.Format("{0,-20} {1,-7} {2,-7}", sensor.ToString(), phonons.Count, incomingPhonons.Count);
		}

	}
}
