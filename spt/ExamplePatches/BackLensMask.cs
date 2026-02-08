using UnityEngine;
using UnityEngine.Rendering;

namespace NonPipScopes.ExamplePatches
{
	// back lens mask renders depth only optic lens mask
	// before player and weapon meshes (on BeforeGBuffer event),
	// so they do not clip into view
	public class BackLensMask : MonoBehaviour
	{
		private CommandBuffer _commandBuffer;

		public void Init(Transform backLensTransform)
		{
            var backLens = backLensTransform.gameObject;
            var backLensMeshFilter = backLens.GetComponent<MeshFilter>();
            var backLensMeshRenderer = backLens.GetComponent<MeshRenderer>();

			gameObject.layer = backLens.layer;

			var _transform = transform;
            _transform.parent = backLensTransform.parent;
            _transform.localPosition = backLensTransform.localPosition;
            _transform.localRotation = backLensTransform.localRotation;
            _transform.localScale = backLensTransform.localScale;

			var _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.mesh = backLensMeshFilter.mesh;

			var _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.material = new Material(Plugin.Instance.DepthOnlyShader);

			_commandBuffer = new CommandBuffer();
			_commandBuffer.name = "BackLensMask";
			_commandBuffer.DrawRenderer(_meshRenderer, _meshRenderer.material);

			var camera = CameraClass.Instance.Camera;
			camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, _commandBuffer);
		}

		public void OnDestroy()
		{
			var camera = CameraClass.Instance.Camera;
			camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, _commandBuffer);
			_commandBuffer.Release();
            _commandBuffer = null;
		}
	}
}
