﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DMagic.Part_Modules
{
	class DMSeismicHammer : DMBasicScienceModule, IDMSeismometer
	{
		[KSPField]
		public string hammerAnimation = "";
		[KSPField]
		public float baseExperimentValue = 0.4f;
		[KSPField(guiActive = false)]
		public string scoreString = "0%";

		private Dictionary<DMSeismicSensor, Vector2> nearbySensors = new Dictionary<DMSeismicSensor, Vector2>();

		private Animation Anim;
		private string failMessage;

		public int sensorsInRange
		{
			get { return nearbySensors.Count; }
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (!string.IsNullOrEmpty(hammerAnimation))
				Anim = part.FindModelAnimators(hammerAnimation)[0];

			base.OnStart(state);

			if (IsDeployed)
				experimentArm = true;
			else
				experimentArm = false;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
		}

		private void Update()
		{
			base.EventsCheck();

			scoreString = experimentScore.ToString("P0");
		}

		#region Animator

		//Controls the main, door-opening animation
		private void animator(float speed, float time, Animation a, string name)
		{
			if (a != null)
			{
				a[name].speed = speed;
				if (!a.IsPlaying(name))
				{
					a[name].normalizedTime = time;
					a.Blend(name, 1f);
				}
			}
		}

		public override void deployEvent()
		{
			base.deployEvent();
			experimentArm = true;
		}

		public override void retractEvent()
		{
			base.retractEvent();
			experimentArm = false;
		}


		#endregion

		#region Science Setup

		public override void DeployExperiment()
		{
			if (!canConduct())
			{
				ScreenMessages.PostScreenMessage(failMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			getScienceData(DMAsteroidScience.AsteroidGrappled);
		}

		private void getScienceData(bool asteroid)
		{
			ScienceData data = DMSeismicHandler.makeData(this, part.flightID, exp, experimentID, vessel.mainBody, vessel, false, asteroid);

			if (data == null)
				return;

			scienceReports.Add(data);
			Deployed = true;
			ReviewData();
		}

		private bool canConduct()
		{
			failMessage = "";
			if (Inoperable)
			{
				failMessage = "Experiment is no longer functional; must be reset at a science lab or returned to Kerbin";
				return false;
			}
			else if (Deployed)
			{
				failMessage = customFailMessage;
				return false;
			}
			else if (scienceReports.Count > 0)
			{
				failMessage = customFailMessage;
				return false;
			}
			else if (vessel.situation != Vessel.Situations.LANDED && vessel.situation != Vessel.Situations.PRELAUNCH && !DMAsteroidScience.AsteroidGrappled)
			{
				failMessage = customFailMessage;
				return false;
			}
			else if (FlightGlobals.ActiveVessel.isEVA)
			{
				if (!ScienceUtil.RequiredUsageExternalAvailable(part.vessel, FlightGlobals.ActiveVessel, (ExperimentUsageReqs)usageReqMaskExternal, exp, ref usageReqMessage))
				{
					failMessage = usageReqMessage;
					return false;
				}
				else
					return true;
			}
			else
				return true;
		}

		#endregion

		#region IDMSeismometer

		public void addSeismometer(IDMSeismometer s, Vector2 v)
		{
			if (!nearbySensors.ContainsKey((DMSeismicSensor)s))
				nearbySensors.Add((DMSeismicSensor)s, v);
			else
				nearbySensors[(DMSeismicSensor)s] = v;
		}

		public void removeSeismometer(IDMSeismometer s)
		{
			if (nearbySensors.ContainsKey((DMSeismicSensor)s))
				nearbySensors.Remove((DMSeismicSensor)s);
		}

		public void updateScore()
		{
			if (nearbySensors.Count <= 0)
			{
				experimentScore = baseExperimentValue;
				return;
			}

			if (nearbySensors.Count >= 5)
			{
				if (nearbySensors.Where(s => s.Value.x > 10).Count() >= 2)
				{
					experimentScore = 1f;
					return;
				}
			}

			float nearbySensorBaseScore = Math.Max(0.2f, 0.1f * nearbySensors.Count);

			float nearSensorScore = 0f;
			float? nearSensorAngle = null;
			float farSensorScore = 0f;
			float? farSensorAngle = null;

			for (int i = 0; i < nearbySensors.Count; i++)
			{
				Vector2 v = nearbySensors.ElementAt(i).Value;

				if (v.x < 2500)
				{
					float distanceScore = 0f;
					float angleScore = 0f;

					if (v.x < 10)
					{
						distanceScore = 0f;
						angleScore = 0f;
					}
					else
					{
						if (v.x < 1500)
							distanceScore = ((v.x - 10) / 1490f) * 0.15f;
						else if (v.x >= 1500)
							distanceScore = 0.15f;

						if (farSensorAngle == null)
							angleScore = 0.05f;
						else
						{
							float angleDelta = angleDifference(v.y, (float)farSensorAngle);

							if (angleDelta < 30)
								angleScore = 0f;
							else if (angleDelta < 120)
								angleScore = ((angleDelta - 30) / 90) * 0.05f;
							else
								angleScore = 0.05f;
						}
					}

					float totalScore = distanceScore + angleScore;

					if (totalScore > nearSensorScore)
					{
						nearSensorScore = totalScore;
						nearSensorAngle = v.y;
					}
				}
				else
				{
					float distanceScore = 0f;
					float angleScore = 0f;

					if (v.x < 4000)
						distanceScore = ((v.x - 2500) / 2500) * 0.15f;
					else
						distanceScore = 0.15f;

					if (nearSensorAngle == null)
						angleScore = 0.05f;
					else
					{
						float angleDelta = angleDifference(v.y, (float)nearSensorAngle);

						if (angleDelta < 30)
							angleScore = 0f;
						else if (angleDelta < 120)
							angleScore = ((angleDelta - 30) / 90) * 0.05f;
						else
							angleScore = 0.05f;
					}

					float totalScore = distanceScore + angleScore;

					if (totalScore > farSensorScore)
					{
						farSensorScore = totalScore;
						farSensorAngle = v.y;
					}
				}

				experimentScore = Mathf.Clamp(baseExperimentValue + nearbySensorBaseScore + nearSensorScore + farSensorScore, 0f, 1f);
			}
		}

		public float experimentScore { get; set; }

		public bool experimentArm { get; set; }

		#endregion

		private float angleDifference(float angle1, float angle2)
		{
			float f = angle1 - angle2;
			while (f < -180)
				f += 360;
			while (f > 180)
				f -= 360;

			return f;
		}

	}
}
