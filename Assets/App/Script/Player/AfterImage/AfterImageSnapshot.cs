using UnityEngine;

public sealed class AfterImageSnapshot
{
    private readonly Mesh[] _meshes;
    private readonly Vector3[] _positions;
    private readonly Quaternion[] _rotations;
    private readonly bool[] _validSlots;
    private readonly RenderParams[] _renderParams;
    private readonly MaterialPropertyBlock[] _propertyBlocks;

    private float _age;
    private float _lifeTime;

    private float _scaleMultiplier;
    private bool _shrinkWithFade;
    private float _minimumFadeScaleRate;

    private Vector3 _referenceForward;

    public bool IsAlive => _age < _lifeTime;

    public int SlotCount => _meshes != null ? _meshes.Length : 0;

    public AfterImageSnapshot(int rendererCount)
    {
        rendererCount = Mathf.Max(0, rendererCount);

        _meshes = new Mesh[rendererCount];
        _positions = new Vector3[rendererCount];
        _rotations = new Quaternion[rendererCount];
        _validSlots = new bool[rendererCount];
        _renderParams = new RenderParams[rendererCount];
        _propertyBlocks = new MaterialPropertyBlock[rendererCount];

        for (int i = 0; i < rendererCount; i++)
        {
            _meshes[i] = new Mesh();
            _meshes[i].name = "Rugby AfterImage Baked Mesh";

            _positions[i] = Vector3.zero;
            _rotations[i] = Quaternion.identity;
            _validSlots[i] = false;

            _propertyBlocks[i] = new MaterialPropertyBlock();
        }

        _age = 0.0f;
        _lifeTime = 0.0f;

        _scaleMultiplier = 1.0f;
        _shrinkWithFade = false;
        _minimumFadeScaleRate = 0.0f;

        _referenceForward = Vector3.forward;
    }

    public void Capture(
        SkinnedMeshRenderer[] renderers,
        Material material,
        float lifeTime,
        float scaleMultiplier,
        bool shrinkWithFade,
        float minimumFadeScaleRate,
        Transform directionReferenceRoot
    )
    {
        _age = 0.0f;
        _lifeTime = Mathf.Max(0.01f, lifeTime);

        _scaleMultiplier = Mathf.Max(0.0f, scaleMultiplier);
        _shrinkWithFade = shrinkWithFade;
        _minimumFadeScaleRate = Mathf.Clamp01(minimumFadeScaleRate);

        for (int i = 0; i < _validSlots.Length; i++)
        {
            _validSlots[i] = false;
            _positions[i] = Vector3.zero;
            _rotations[i] = Quaternion.identity;
        }

        if (directionReferenceRoot != null)
        {
            _referenceForward = directionReferenceRoot.forward;
        }
        else if (renderers != null && renderers.Length > 0 && renderers[0] != null)
        {
            _referenceForward = renderers[0].transform.forward;
        }
        else
        {
            _referenceForward = Vector3.forward;
        }

        if (!IsFiniteVector3(_referenceForward) || _referenceForward.sqrMagnitude <= 0.0001f)
        {
            _referenceForward = Vector3.forward;
        }

        _referenceForward.Normalize();

        if (renderers == null)
        {
            return;
        }

        int count = Mathf.Min(renderers.Length, _meshes.Length);

        for (int i = 0; i < count; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];

            if (renderer == null)
            {
                _validSlots[i] = false;
                _positions[i] = Vector3.zero;
                _rotations[i] = Quaternion.identity;
                continue;
            }

            Material useMaterial = material != null ? material : renderer.sharedMaterial;

            if (useMaterial == null)
            {
                _validSlots[i] = false;
                _positions[i] = Vector3.zero;
                _rotations[i] = Quaternion.identity;
                continue;
            }

            _renderParams[i] = new RenderParams(useMaterial);
            _renderParams[i].matProps = _propertyBlocks[i];

            renderer.BakeMesh(_meshes[i]);

            Vector3 position = renderer.transform.position;
            Quaternion rotation = renderer.transform.rotation;

            if (!IsFiniteVector3(position))
            {
                _validSlots[i] = false;
                _positions[i] = Vector3.zero;
                _rotations[i] = Quaternion.identity;
                continue;
            }

            if (!IsValidQuaternion(rotation))
            {
                rotation = Quaternion.identity;
            }

            /*
             * MeshRoot側のscale = 3などが二重に掛からないように、
             * localToWorldMatrixは使わず、位置と回転だけ保持する。
             *
             * 残像専用のサイズ調整はRender()側で行う。
             */
            _positions[i] = position;
            _rotations[i] = rotation;
            _validSlots[i] = true;
        }
    }

    public void Tick(float deltaTime)
    {
        _age += Mathf.Max(0.0f, deltaTime);
    }

    public void Render(
        Transform cameraTransform,
        bool useCameraDistanceFade,
        float closeFadeDistance,
        float fullVisibleDistance,
        float closeDistanceAlphaRate,
        bool scaleDownWhenCloseToCamera,
        float closeDistanceScaleRate,
        bool useBackViewFade,
        Transform directionReferenceRoot,
        bool useHorizontalDirectionOnly,
        float strongestBackViewDot,
        float fullVisibleBackViewDot,
        float backViewAlphaRate
    )
    {
        float normalized = Mathf.Clamp01(_age / Mathf.Max(0.01f, _lifeTime));
        float fade = 1.0f - normalized;

        float fadeScaleRate = 1.0f;

        if (_shrinkWithFade)
        {
            fadeScaleRate = Mathf.Lerp(_minimumFadeScaleRate, 1.0f, fade);
        }

        Vector3 cameraPosition = Vector3.zero;
        bool hasCamera = cameraTransform != null;

        if (hasCamera)
        {
            cameraPosition = cameraTransform.position;

            if (!IsFiniteVector3(cameraPosition))
            {
                hasCamera = false;
                cameraPosition = Vector3.zero;
            }
        }

        Vector3 referenceForward = _referenceForward;

        if (directionReferenceRoot != null)
        {
            referenceForward = directionReferenceRoot.forward;
        }

        if (useHorizontalDirectionOnly)
        {
            referenceForward.y = 0.0f;
        }

        if (!IsFiniteVector3(referenceForward) || referenceForward.sqrMagnitude <= 0.0001f)
        {
            referenceForward = Vector3.forward;
        }

        referenceForward.Normalize();

        float fixedCloseFadeDistance = Mathf.Max(0.001f, closeFadeDistance);
        float fixedFullVisibleDistance = Mathf.Max(fixedCloseFadeDistance + 0.001f, fullVisibleDistance);

        for (int i = 0; i < _meshes.Length; i++)
        {
            if (!_validSlots[i])
            {
                continue;
            }

            Mesh mesh = _meshes[i];

            if (mesh == null)
            {
                continue;
            }

            Quaternion rotation = _rotations[i];

            if (!IsValidQuaternion(rotation))
            {
                rotation = Quaternion.identity;
            }

            Vector3 position = _positions[i];

            if (!IsFiniteVector3(position))
            {
                continue;
            }

            float perMeshAlphaRate = 1.0f;
            float perMeshScaleRate = 1.0f;

            if (hasCamera)
            {
                Vector3 toCamera = cameraPosition - position;

                if (IsFiniteVector3(toCamera))
                {
                    float distanceToCamera = toCamera.magnitude;

                    if (useCameraDistanceFade)
                    {
                        float nearT = Mathf.InverseLerp(
                            fixedCloseFadeDistance,
                            fixedFullVisibleDistance,
                            distanceToCamera
                        );

                        nearT = Mathf.Clamp01(nearT);
                        perMeshAlphaRate *= Mathf.Lerp(Mathf.Clamp01(closeDistanceAlphaRate), 1.0f, nearT);
                    }

                    if (scaleDownWhenCloseToCamera)
                    {
                        float nearT = Mathf.InverseLerp(
                            fixedCloseFadeDistance,
                            fixedFullVisibleDistance,
                            distanceToCamera
                        );

                        nearT = Mathf.Clamp01(nearT);
                        perMeshScaleRate *= Mathf.Lerp(Mathf.Clamp01(closeDistanceScaleRate), 1.0f, nearT);
                    }

                    if (useBackViewFade && toCamera.sqrMagnitude > 0.0001f)
                    {
                        Vector3 directionToCamera = toCamera.normalized;

                        if (useHorizontalDirectionOnly)
                        {
                            directionToCamera.y = 0.0f;

                            if (directionToCamera.sqrMagnitude > 0.0001f)
                            {
                                directionToCamera.Normalize();
                            }
                        }

                        if (IsFiniteVector3(directionToCamera) && directionToCamera.sqrMagnitude > 0.0001f)
                        {
                            float dot = Vector3.Dot(referenceForward, directionToCamera);

                            float backViewT = Mathf.InverseLerp(
                                strongestBackViewDot,
                                fullVisibleBackViewDot,
                                dot
                            );

                            backViewT = Mathf.Clamp01(backViewT);
                            perMeshAlphaRate *= Mathf.Lerp(Mathf.Clamp01(backViewAlphaRate), 1.0f, backViewT);
                        }
                    }
                }
            }

            float finalFade = fade * perMeshAlphaRate;
            finalFade = Mathf.Clamp01(finalFade);

            if (finalFade <= 0.001f)
            {
                continue;
            }

            _propertyBlocks[i].SetFloat("_Fade", finalFade);

            RenderParams renderParams = _renderParams[i];
            renderParams.matProps = _propertyBlocks[i];

            float currentScale = _scaleMultiplier * fadeScaleRate * perMeshScaleRate;

            if (!float.IsFinite(currentScale))
            {
                currentScale = 1.0f;
            }

            currentScale = Mathf.Max(0.0f, currentScale);

            if (currentScale <= 0.0001f)
            {
                continue;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                rotation,
                Vector3.one * currentScale
            );

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Graphics.RenderMesh(renderParams, mesh, subMeshIndex, matrix);
            }
        }
    }

    public void Reset()
    {
        _age = 0.0f;
        _lifeTime = 0.0f;

        _scaleMultiplier = 1.0f;
        _shrinkWithFade = false;
        _minimumFadeScaleRate = 0.0f;
        _referenceForward = Vector3.forward;

        for (int i = 0; i < _validSlots.Length; i++)
        {
            _validSlots[i] = false;
            _positions[i] = Vector3.zero;
            _rotations[i] = Quaternion.identity;
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _meshes.Length; i++)
        {
            if (_meshes[i] != null)
            {
                Object.Destroy(_meshes[i]);
                _meshes[i] = null;
            }
        }
    }

    private static bool IsValidQuaternion(Quaternion rotation)
    {
        if (!float.IsFinite(rotation.x))
        {
            return false;
        }

        if (!float.IsFinite(rotation.y))
        {
            return false;
        }

        if (!float.IsFinite(rotation.z))
        {
            return false;
        }

        if (!float.IsFinite(rotation.w))
        {
            return false;
        }

        float lengthSqr =
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w;

        return lengthSqr > 0.000001f;
    }

    private static bool IsFiniteVector3(Vector3 value)
    {
        if (!float.IsFinite(value.x))
        {
            return false;
        }

        if (!float.IsFinite(value.y))
        {
            return false;
        }

        if (!float.IsFinite(value.z))
        {
            return false;
        }

        return true;
    }
}