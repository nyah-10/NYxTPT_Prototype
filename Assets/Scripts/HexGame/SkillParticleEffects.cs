using UnityEngine;

public static class SkillParticleEffects
{
    private const int SortingOrder = 40;

    public static void Play(SkillDefinition skill, Vector3 source, Vector3 target)
    {
        if (skill == null) return;

        switch (skill.name)
        {
            case "SwordStrike": PlaySwordStrike(target, source, target); break;
            case "ArcaneBolt": PlayArcaneBolt(source, target); break;
            case "FirstAid": PlayFirstAid(source); break;
            case "Leap": PlayLeapImpact(target); break;
        }
    }

    private static void PlaySwordStrike(Vector3 position, Vector3 source, Vector3 target)
    {
        ParticleSystem particles = CreateSystem("Sword Strike VFX", position, "SkillEffects/sword_slash_particle");
        Vector2 direction = target - source;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f;

        for (int i = 0; i < 3; i++)
        {
            ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
            {
                startLifetime = .22f + i * .04f,
                startSize = 1.18f + i * .16f,
                startColor = new Color(1f, 1f, 1f, 1f - i * .22f),
                rotation = (angle + (i - 1) * 8f) * Mathf.Deg2Rad
            };
            particles.Emit(emission, 1);
        }
    }

    private static void PlayArcaneBolt(Vector3 source, Vector3 target)
    {
        ParticleSystem particles = CreateSystem("Arcane Bolt VFX", source, "SkillEffects/arcane_mote_particle");
        Vector3 direction = (target - source).normalized;
        float distance = Vector3.Distance(source, target);
        float lifetime = Mathf.Max(.18f, distance / 6.5f);

        for (int i = 0; i < 7; i++)
        {
            Vector3 tangent = new Vector3(-direction.y, direction.x, 0f) * Random.Range(-.22f, .22f);
            ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
            {
                velocity = direction * Random.Range(5.8f, 7.2f) + tangent,
                startLifetime = lifetime,
                startSize = i == 0 ? .52f : Random.Range(.12f, .25f),
                startColor = i == 0 ? Color.white : new Color(.5f, .65f, 1f, .72f)
            };
            particles.Emit(emission, 1);
        }
    }

    private static void PlayFirstAid(Vector3 position)
    {
        ParticleSystem particles = CreateSystem("First Aid VFX", position, "SkillEffects/healing_pulse_particle");
        for (int i = 0; i < 11; i++)
        {
            float angle = i * Mathf.PI * 2f / 11f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
            {
                position = radial * Random.Range(.08f, .28f),
                velocity = radial * .35f + Vector3.up * Random.Range(.45f, .95f),
                startLifetime = Random.Range(.55f, 1f),
                startSize = i == 0 ? .62f : Random.Range(.12f, .27f),
                startColor = new Color(.7f, 1f, .72f, Random.Range(.62f, 1f)),
                rotation = Random.Range(0f, Mathf.PI * 2f)
            };
            particles.Emit(emission, 1);
        }
    }

    private static void PlayLeapImpact(Vector3 position)
    {
        ParticleSystem particles = CreateSystem("Leap Impact VFX", position, "SkillEffects/leap_impact_particle");
        for (int i = 0; i < 14; i++)
        {
            float angle = i * Mathf.PI * 2f / 14f + Random.Range(-.12f, .12f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
            {
                velocity = direction * Random.Range(1.1f, 2.5f),
                startLifetime = Random.Range(.28f, .62f),
                startSize = Random.Range(.16f, .38f),
                startColor = new Color(.68f, .9f, 1f, Random.Range(.55f, 1f)),
                rotation = (angle - Mathf.PI * .5f)
            };
            particles.Emit(emission, 1);
        }
    }

    private static ParticleSystem CreateSystem(string objectName, Vector3 position, string texturePath)
    {
        GameObject effect = new GameObject(objectName);
        effect.transform.position = position + Vector3.back * .1f;
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.9f, .7f), new GradientAlphaKey(0f, 1f) },
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }
        });

        Texture2D texture = Resources.Load<Texture2D>(texturePath);
        Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader) { mainTexture = texture };
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = SortingOrder;
        renderer.material = material;
        effect.AddComponent<ParticleMaterialCleanup>().Material = material;
        particles.Play();
        return particles;
    }
}

public sealed class ParticleMaterialCleanup : MonoBehaviour
{
    public Material Material;

    private void OnDestroy()
    {
        if (Material != null) Destroy(Material);
    }
}
