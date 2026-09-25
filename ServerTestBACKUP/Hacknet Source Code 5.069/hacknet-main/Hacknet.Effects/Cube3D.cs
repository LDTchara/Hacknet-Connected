using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Effects;

internal class Cube3D
{
	private const int NUM_VERTICES = 36;

	private const int NUM_INDICIES = 14;

	private static VertexPositionNormalTexture[] verts;

	private static VertexBuffer vBuffer;

	private static IndexBuffer ib;

	private static BasicEffect wireframeEfect;

	private static RasterizerState wireframeRaster;

	public static void Initilize(GraphicsDevice gd)
	{
		ConstructCube();
		vBuffer = new VertexBuffer(gd, VertexPositionNormalTexture.VertexDeclaration, 36, BufferUsage.WriteOnly);
		vBuffer.SetData(verts);
		gd.SetVertexBuffer(vBuffer);
		ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, 14, BufferUsage.WriteOnly);
		ib.SetData(new short[14]
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11, 12, 13
		});
		gd.Indices = ib;
		wireframeRaster = new RasterizerState();
		wireframeRaster.FillMode = FillMode.WireFrame;
		wireframeRaster.CullMode = CullMode.None;
		wireframeEfect = new BasicEffect(gd);
		wireframeEfect.Projection = Matrix.CreatePerspectiveFieldOfView((float)Math.PI / 4f, wireframeEfect.GraphicsDevice.Viewport.AspectRatio, 0.01f, 3000f);
	}

	private static void ResetBuffers()
	{
		GraphicsDevice graphicsDevice = wireframeEfect.GraphicsDevice;
		graphicsDevice.SetVertexBuffer(vBuffer);
		graphicsDevice.Indices = ib;
	}

	public static void RenderWireframe(Vector3 position, float scale, Vector3 rotation, Color color)
	{
		RenderWireframe(position, scale, rotation, color, new Vector3(0f, 0f, 20f));
	}

	public static void RenderWireframe(Vector3 position, float scale, Vector3 rotation, Color color, Vector3 cameraOffset)
	{
		scale = Math.Max(0.001f, scale);
		wireframeEfect.DiffuseColor = Utils.ColorToVec3(color);
		wireframeEfect.GraphicsDevice.BlendState = BlendState.Opaque;
		wireframeEfect.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
		wireframeEfect.GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
		ResetBuffers();
		RasterizerState rasterizerState = wireframeEfect.GraphicsDevice.RasterizerState;
		wireframeEfect.GraphicsDevice.RasterizerState = wireframeRaster;
		Vector3 vector = new Vector3(0f, 0f, 0f);
		Matrix world = Matrix.CreateTranslation(-vector) * Matrix.CreateScale(scale) * Matrix.CreateRotationY(rotation.Y) * Matrix.CreateRotationX(rotation.X) * Matrix.CreateRotationZ(rotation.Z) * Matrix.CreateTranslation(position);
		wireframeEfect.World = world;
		wireframeEfect.View = Matrix.CreateLookAt(cameraOffset, position, Vector3.Up);
		try
		{
			foreach (EffectPass pass in wireframeEfect.CurrentTechnique.Passes)
			{
				pass.Apply();
				wireframeEfect.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 36);
			}
		}
		catch (NotSupportedException)
		{
			Console.WriteLine("Not supported happened");
		}
		wireframeEfect.GraphicsDevice.RasterizerState = rasterizerState;
	}

	private static void ConstructCube()
	{
		verts = new VertexPositionNormalTexture[36];
		Vector3 position = new Vector3(-1f, 1f, -1f);
		Vector3 position2 = new Vector3(-1f, 1f, 1f);
		Vector3 position3 = new Vector3(1f, 1f, -1f);
		Vector3 position4 = new Vector3(1f, 1f, 1f);
		Vector3 position5 = new Vector3(-1f, -1f, -1f);
		Vector3 position6 = new Vector3(-1f, -1f, 1f);
		Vector3 position7 = new Vector3(1f, -1f, -1f);
		Vector3 position8 = new Vector3(1f, -1f, 1f);
		Vector3 normal = new Vector3(0f, 0f, 1f);
		Vector3 normal2 = new Vector3(0f, 0f, -1f);
		Vector3 normal3 = new Vector3(0f, 1f, 0f);
		Vector3 normal4 = new Vector3(0f, -1f, 0f);
		Vector3 normal5 = new Vector3(-1f, 0f, 0f);
		Vector3 normal6 = new Vector3(1f, 0f, 0f);
		Vector2 textureCoordinate = new Vector2(1f, 0f);
		Vector2 textureCoordinate2 = new Vector2(0f, 0f);
		Vector2 textureCoordinate3 = new Vector2(1f, 1f);
		Vector2 textureCoordinate4 = new Vector2(0f, 1f);
		ref VertexPositionNormalTexture reference = ref verts[0];
		reference = new VertexPositionNormalTexture(position, normal, textureCoordinate);
		ref VertexPositionNormalTexture reference2 = ref verts[1];
		reference2 = new VertexPositionNormalTexture(position5, normal, textureCoordinate3);
		ref VertexPositionNormalTexture reference3 = ref verts[2];
		reference3 = new VertexPositionNormalTexture(position3, normal, textureCoordinate2);
		ref VertexPositionNormalTexture reference4 = ref verts[3];
		reference4 = new VertexPositionNormalTexture(position5, normal, textureCoordinate3);
		ref VertexPositionNormalTexture reference5 = ref verts[4];
		reference5 = new VertexPositionNormalTexture(position7, normal, textureCoordinate4);
		ref VertexPositionNormalTexture reference6 = ref verts[5];
		reference6 = new VertexPositionNormalTexture(position3, normal, textureCoordinate2);
		ref VertexPositionNormalTexture reference7 = ref verts[6];
		reference7 = new VertexPositionNormalTexture(position2, normal2, textureCoordinate2);
		ref VertexPositionNormalTexture reference8 = ref verts[7];
		reference8 = new VertexPositionNormalTexture(position4, normal2, textureCoordinate);
		ref VertexPositionNormalTexture reference9 = ref verts[8];
		reference9 = new VertexPositionNormalTexture(position6, normal2, textureCoordinate4);
		ref VertexPositionNormalTexture reference10 = ref verts[9];
		reference10 = new VertexPositionNormalTexture(position6, normal2, textureCoordinate4);
		ref VertexPositionNormalTexture reference11 = ref verts[10];
		reference11 = new VertexPositionNormalTexture(position4, normal2, textureCoordinate);
		ref VertexPositionNormalTexture reference12 = ref verts[11];
		reference12 = new VertexPositionNormalTexture(position8, normal2, textureCoordinate3);
		ref VertexPositionNormalTexture reference13 = ref verts[12];
		reference13 = new VertexPositionNormalTexture(position, normal3, textureCoordinate3);
		ref VertexPositionNormalTexture reference14 = ref verts[13];
		reference14 = new VertexPositionNormalTexture(position4, normal3, textureCoordinate2);
		ref VertexPositionNormalTexture reference15 = ref verts[14];
		reference15 = new VertexPositionNormalTexture(position2, normal3, textureCoordinate);
		ref VertexPositionNormalTexture reference16 = ref verts[15];
		reference16 = new VertexPositionNormalTexture(position, normal3, textureCoordinate3);
		ref VertexPositionNormalTexture reference17 = ref verts[16];
		reference17 = new VertexPositionNormalTexture(position3, normal3, textureCoordinate4);
		ref VertexPositionNormalTexture reference18 = ref verts[17];
		reference18 = new VertexPositionNormalTexture(position4, normal3, textureCoordinate2);
		ref VertexPositionNormalTexture reference19 = ref verts[18];
		reference19 = new VertexPositionNormalTexture(position5, normal4, textureCoordinate);
		ref VertexPositionNormalTexture reference20 = ref verts[19];
		reference20 = new VertexPositionNormalTexture(position6, normal4, textureCoordinate3);
		ref VertexPositionNormalTexture reference21 = ref verts[20];
		reference21 = new VertexPositionNormalTexture(position8, normal4, textureCoordinate4);
		ref VertexPositionNormalTexture reference22 = ref verts[21];
		reference22 = new VertexPositionNormalTexture(position5, normal4, textureCoordinate);
		ref VertexPositionNormalTexture reference23 = ref verts[22];
		reference23 = new VertexPositionNormalTexture(position8, normal4, textureCoordinate4);
		ref VertexPositionNormalTexture reference24 = ref verts[23];
		reference24 = new VertexPositionNormalTexture(position7, normal4, textureCoordinate2);
		ref VertexPositionNormalTexture reference25 = ref verts[24];
		reference25 = new VertexPositionNormalTexture(position, normal5, textureCoordinate2);
		ref VertexPositionNormalTexture reference26 = ref verts[25];
		reference26 = new VertexPositionNormalTexture(position6, normal5, textureCoordinate3);
		ref VertexPositionNormalTexture reference27 = ref verts[26];
		reference27 = new VertexPositionNormalTexture(position5, normal5, textureCoordinate4);
		ref VertexPositionNormalTexture reference28 = ref verts[27];
		reference28 = new VertexPositionNormalTexture(position2, normal5, textureCoordinate);
		ref VertexPositionNormalTexture reference29 = ref verts[28];
		reference29 = new VertexPositionNormalTexture(position6, normal5, textureCoordinate3);
		ref VertexPositionNormalTexture reference30 = ref verts[29];
		reference30 = new VertexPositionNormalTexture(position, normal5, textureCoordinate2);
		ref VertexPositionNormalTexture reference31 = ref verts[30];
		reference31 = new VertexPositionNormalTexture(position3, normal6, textureCoordinate);
		ref VertexPositionNormalTexture reference32 = ref verts[31];
		reference32 = new VertexPositionNormalTexture(position7, normal6, textureCoordinate3);
		ref VertexPositionNormalTexture reference33 = ref verts[32];
		reference33 = new VertexPositionNormalTexture(position8, normal6, textureCoordinate4);
		ref VertexPositionNormalTexture reference34 = ref verts[33];
		reference34 = new VertexPositionNormalTexture(position4, normal6, textureCoordinate2);
		ref VertexPositionNormalTexture reference35 = ref verts[34];
		reference35 = new VertexPositionNormalTexture(position3, normal6, textureCoordinate);
		ref VertexPositionNormalTexture reference36 = ref verts[35];
		reference36 = new VertexPositionNormalTexture(position8, normal6, textureCoordinate4);
	}
}
