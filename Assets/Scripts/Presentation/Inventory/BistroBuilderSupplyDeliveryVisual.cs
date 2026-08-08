using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representación temporal y deliberadamente sencilla de un repartidor.
/// Se construye con primitivas runtime para no introducir todavía un sistema
/// de personajes, animación, personal o assets logísticos específicos.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplyDeliveryVisual : MonoBehaviour
{
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private GameObject boxesRoot;

    public bool HasBoxes => boxesRoot != null && boxesRoot.activeSelf;

    public static BistroBuilderSupplyDeliveryVisual Create(
        Transform parent,
        Vector3 worldPosition
    )
    {
        var root = new GameObject("BB_DeliveryPerson_Temporary");
        root.transform.SetParent(parent, true);
        root.transform.position = worldPosition;

        BistroBuilderSupplyDeliveryVisual visual =
            root.AddComponent<BistroBuilderSupplyDeliveryVisual>();
        visual.BuildPlaceholderGeometry();
        return visual;
    }

    public IEnumerator MoveTo(
        Vector3 destination,
        float movementSpeed,
        float arrivalDistance
    )
    {
        float speed = Mathf.Max(0.1f, movementSpeed);
        float arrival = Mathf.Max(0.01f, arrivalDistance);

        while (this != null &&
               Vector3.Distance(transform.position, destination) > arrival)
        {
            Vector3 direction = destination - transform.position;
            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(flatDirection.normalized, Vector3.up),
                    Mathf.Clamp01(Time.deltaTime * 10f)
                );
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                speed * Time.deltaTime
            );
            yield return null;
        }

        if (this != null)
        {
            transform.position = destination;
        }
    }

    public void SetBoxesVisible(bool visible)
    {
        if (boxesRoot != null)
        {
            boxesRoot.SetActive(visible);
        }
    }

    private void BuildPlaceholderGeometry()
    {
        Material uniform = CreateRuntimeMaterial(
            new Color(0.16f, 0.28f, 0.42f, 1f)
        );
        Material skin = CreateRuntimeMaterial(
            new Color(0.76f, 0.60f, 0.46f, 1f)
        );
        Material cardboard = CreateRuntimeMaterial(
            new Color(0.52f, 0.31f, 0.13f, 1f)
        );

        GameObject body = CreatePrimitiveChild(
            PrimitiveType.Capsule,
            "Body",
            transform,
            new Vector3(0f, 0.92f, 0f),
            new Vector3(0.42f, 0.62f, 0.42f),
            uniform
        );
        body.transform.localRotation = Quaternion.identity;

        CreatePrimitiveChild(
            PrimitiveType.Sphere,
            "Head",
            transform,
            new Vector3(0f, 1.72f, 0f),
            new Vector3(0.34f, 0.34f, 0.34f),
            skin
        );

        boxesRoot = new GameObject("Boxes");
        boxesRoot.transform.SetParent(transform, false);
        boxesRoot.transform.localPosition = Vector3.zero;

        CreatePrimitiveChild(
            PrimitiveType.Cube,
            "Box_Lower",
            boxesRoot.transform,
            new Vector3(0f, 0.82f, 0.38f),
            new Vector3(0.56f, 0.38f, 0.42f),
            cardboard
        );
        CreatePrimitiveChild(
            PrimitiveType.Cube,
            "Box_Upper",
            boxesRoot.transform,
            new Vector3(0.08f, 1.15f, 0.38f),
            new Vector3(0.46f, 0.30f, 0.36f),
            cardboard
        );
    }

    private Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader)
        {
            name = "__BB_22B_RuntimeMaterial__",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        runtimeMaterials.Add(material);
        return material;
    }

    private static GameObject CreatePrimitiveChild(
        PrimitiveType primitiveType,
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material
    )
    {
        GameObject child = GameObject.CreatePrimitive(primitiveType);
        child.name = objectName;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = localScale;

        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        Renderer renderer = child.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        return child;
    }

    private void OnDestroy()
    {
        for (int index = 0; index < runtimeMaterials.Count; index++)
        {
            Material material = runtimeMaterials[index];
            if (material != null)
            {
                Destroy(material);
            }
        }

        runtimeMaterials.Clear();
    }
}
