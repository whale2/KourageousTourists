using System.Collections;

using UnityEngine;

namespace KourageousTourists
{
	public static class ChuteSupport
	{

		public static bool hasChute(Vessel v)
		{
			return v.evaController.part.Modules.Contains("ModuleEvaChute");
		}

		public static IEnumerator deployChute(Vessel v, float paraglidingDeployDelay, float paraglidingChutePitch) {
			if (!hasChute(v)) {
				KourageousTouristsAddOn.printDebug("No ModuleEvaChute!!! Oops...");
				yield  break;
			}
			KourageousTouristsAddOn.printDebug("checking chute module...");
			ModuleEvaChute chuteModule = (ModuleEvaChute)v.evaController.part.Modules ["ModuleEvaChute"];
			KourageousTouristsAddOn.printDebug(string.Format("deployment state: {0}; enabled: {1}", chuteModule.deploymentSafeState, chuteModule.enabled));
			chuteModule.deploymentSafeState = ModuleParachute.deploymentSafeStates.UNSAFE; // FIXME: is it immediate???

			KourageousTouristsAddOn.printDebug(string.Format("counting {0} sec...", paraglidingDeployDelay));
			yield return new WaitForSeconds (paraglidingDeployDelay); // 5 seconds to deploy chute. TODO: Make configurable
			KourageousTouristsAddOn.printDebug("Deploying chute");
			chuteModule.Deploy ();

			// Set low forward pitch so uncontrolled kerbal doesn't gain lot of speed
			chuteModule.chuteDefaultForwardPitch = paraglidingChutePitch;
		}
	}
}
