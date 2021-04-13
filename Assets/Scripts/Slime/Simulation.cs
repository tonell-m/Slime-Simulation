using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;
using System.Collections;
using System.Collections.Generic;

public class Simulation : MonoBehaviour
{

	// Properties

	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle, MIDIControlled }

	const int updateKernel = 0;
	const int diffuseMapKernel = 1;

	public ComputeShader compute;
	public ComputeShader drawAgentsCS;

	public SlimeSettings settings;

	[Header("Display Settings")]
	public bool showAgentsOnly;
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	Texture2D colourMapTexture;


	private List<Agent> agentList = new List<Agent>();
	private int totalAgents = 0;


	protected virtual void Start()
	{
		Init();
		transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
		StartCoroutine(UpdateAgentsList());
	}


	void Init()
	{
		// Create render textures
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

		totalAgents = settings.numAgents;

		// Create agents with initial positions and angles
		for (int i = 0; i < totalAgents; i++)
		{
			agentList.Add(spawnAgent());
		}

		RefreshAgentsInSimulation();

		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);
	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	void LateUpdate()
	{
		if (showAgentsOnly)
		{
			ComputeHelper.ClearRenderTexture(displayTexture);

			drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
			ComputeHelper.Dispatch(drawAgentsCS, totalAgents, 1, 1, 0);

		}
		else
		{
			ComputeHelper.CopyRenderTexture(trailMap, displayTexture);
		}
	}

	void RunSimulation()
	{
		var speciesSettings = settings.speciesSettings;
		ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
		compute.SetBuffer(0, "speciesSettings", settingsBuffer);


		// Assign textures
		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);

		// Assign settings
		compute.SetFloat("deltaTime", Time.fixedDeltaTime);
		compute.SetFloat("time", Time.fixedTime);

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);


		ComputeHelper.Dispatch(compute, totalAgents, 1, 1, kernelIndex: updateKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
	}

	void OnDestroy()
	{
		ComputeHelper.Release(agentBuffer, settingsBuffer);
	}

	void RefreshAgentsInSimulation() {
		Agent[] agents = agentList.ToArray();
		totalAgents = agents.Length;

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);

		compute.SetInt("numAgents", totalAgents);
		drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
		drawAgentsCS.SetInt("numAgents", totalAgents);
	}
	
	public struct Agent
	{
		public Vector2 position;
		public float angle;
		public Vector3Int speciesMask;
		int unusedSpeciesChannel;
		public int speciesIndex;
	}


	private Agent spawnAgent() {
		Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
		Vector2 startPos = Vector2.zero;
		float randomAngle = Random.value * Mathf.PI * 2;
		float angle = 0;

		if (settings.spawnMode == SpawnMode.Point)
		{
			startPos = centre;
			angle = randomAngle;
		}
		else if (settings.spawnMode == SpawnMode.Random)
		{
			startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
			angle = randomAngle;
		}
		else if (settings.spawnMode == SpawnMode.InwardCircle)
		{
			startPos = centre + Random.insideUnitCircle * settings.height * 0.2f;
			angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
		}
		else if (settings.spawnMode == SpawnMode.RandomCircle)
		{
			startPos = centre + Random.insideUnitCircle * settings.height * 0.15f;
			angle = randomAngle;
		}
		Vector3Int speciesMask;
		int speciesIndex = 0;
		int numSpecies = settings.speciesSettings.Length;

		if (numSpecies == 1)
		{
			speciesMask = Vector3Int.one;
		}
		else
		{
			int species = Random.Range(1, numSpecies + 1);
			speciesIndex = species - 1;
			speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
		}

		return new Agent() { position = startPos, angle = angle, speciesMask = speciesMask, speciesIndex = speciesIndex };
	}

	private IEnumerator UpdateAgentsList() {
		yield return new WaitForSeconds(2.0f);
		
		const int newAgentsCount = 5000;

		// Get existing agents
		agentList = new List<Agent>(ComputeHelper.GetBufferData<Agent>(agentBuffer));
		
		// Remove the first N agents;
		agentList.RemoveRange(0, newAgentsCount);

		// Create and append new agents
		for (int i = 0; i < newAgentsCount; i++) 
		{
			agentList.Add(spawnAgent());
		}

		// Refresh agents list
		RefreshAgentsInSimulation();

		StartCoroutine(UpdateAgentsList());
	}
}
