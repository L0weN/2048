using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private int m_Width = 4;
    [SerializeField] private int m_Height = 4;
    [SerializeField] private Node m_NodePrefab;
    [SerializeField] private Block m_BlockPrefab;
    [SerializeField] private SpriteRenderer m_BoardPrefab;
    [SerializeField] private List<BlockType> m_BlockTypes;
    [SerializeField] private float m_TravelTime = 0.2f;
    [SerializeField] private int m_WinCondition = 2048;

    [SerializeField] private GameObject m_WinScreen, m_LoseScreen;

    private List<Node> m_Nodes;
    private List<Block> m_Blocks;
    private GameState m_GameState;

    private int m_Round;

    private BlockType GetBlockTypeByValue(int value) => m_BlockTypes.First(t => t.Value == value);

    private void Start()
    {
        ChangeState(GameState.GenerateLevel);
    }

    private void Update()
    {
        if (m_GameState != GameState.WaitingInput) return;

        if (Input.GetKeyDown(KeyCode.W))
        {
            MoveBlocks(Vector2.up);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            MoveBlocks(Vector2.down);
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            MoveBlocks(Vector2.left);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            MoveBlocks(Vector2.right);
        }
    }

    private void ChangeState(GameState newState)
    {
        m_GameState = newState;
        Debug.Log($"State changed to {newState}");

        switch (newState)
        {
            case GameState.GenerateLevel:
                GenerateGrid();
                break;
            case GameState.SpawningBlocks:
                SpawnBlocks(m_Round++ == 0 ? 2 : 1);
                break;
            case GameState.WaitingInput:
                break;
            case GameState.Moving:
                break;
            case GameState.Win:
                m_WinScreen.SetActive(true);
                break;
            case GameState.Lose:
                m_LoseScreen.SetActive(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    void GenerateGrid()
    {
        m_Round = 0;

        m_Nodes = new List<Node>();
        m_Blocks = new List<Block>();

        for (int i = 0; i < m_Width; i++)
        {
            for (int j = 0; j < m_Height; j++)
            {
                var node = Instantiate(m_NodePrefab, new Vector2(i, j), Quaternion.identity);
                m_Nodes.Add(node);
            }
        }

        var center = new Vector2((float)m_Width / 2 - 0.5f, (float)m_Height / 2 - 0.5f);

        var board = Instantiate(m_BoardPrefab, center, Quaternion.identity);
        board.size = new Vector2(m_Width, m_Height);

        Camera.main.transform.position = new Vector3(center.x, center.y, -10);

        ChangeState(GameState.SpawningBlocks);
    }

    void SpawnBlocks(int amount)
    {
        var freeNodes = m_Nodes.Where(n => n.OccupiedBlock == null).OrderBy(b => UnityEngine.Random.value).ToList();

        foreach (var node in freeNodes.Take(amount))
        {
            SpawnBlock(node, UnityEngine.Random.value > 0.8f ? 4 : 2);
        }

        if (freeNodes.Count() == 1)
        {
            ChangeState(GameState.Lose);
            return;
        }

        ChangeState(m_Blocks.Any(b => b.Value == m_WinCondition) ? GameState.Win : GameState.WaitingInput);
    }

    void SpawnBlock(Node node, int value)
    {
        var block = Instantiate(m_BlockPrefab, node.Position, Quaternion.identity);
        block.Init(GetBlockTypeByValue(value));
        block.SetBlock(node);
        m_Blocks.Add(block);
    }

    void MoveBlocks(Vector2 moveDirection)
    {
        ChangeState(GameState.Moving);
        var orderedBlocks = m_Blocks.OrderBy(b => b.Position.x).ThenBy(b => b.Position.y).ToList();
        if (moveDirection == Vector2.right || moveDirection == Vector2.up) orderedBlocks.Reverse();

        foreach (var block in orderedBlocks)
        {
            var next = block.Node;
            do
            {
                block.SetBlock(next);

                var possibleNode = GetNodeAtPosition(next.Position + moveDirection);
                if (possibleNode != null)
                {
                    if (possibleNode.OccupiedBlock != null && possibleNode.OccupiedBlock.CanMerge(block.Value))
                    {
                        block.MergeBlock(possibleNode.OccupiedBlock);
                    }
                    else if (possibleNode.OccupiedBlock == null) next = possibleNode;
                }
            } while (next != block.Node);
        }

        var sequence = DOTween.Sequence();

        foreach (var block in orderedBlocks)
        {
            var movePoint = block.MergingBlock != null ? block.MergingBlock.Node.Position : block.Node.Position;

            sequence.Insert(0, block.transform.DOMove(movePoint, m_TravelTime));
        }
        sequence.OnComplete(() =>
        {
            foreach (var block in orderedBlocks.Where(b => b.MergingBlock != null))
            {
                MergeBlocks(block.MergingBlock, block);
            }

            ChangeState(GameState.SpawningBlocks);
        });

    }

    void MergeBlocks(Block baseBlock, Block mergingBlock)
    {
        SpawnBlock(baseBlock.Node, baseBlock.Value * 2);
        RemoveBlock(baseBlock);
        RemoveBlock(mergingBlock);
    }

    void RemoveBlock(Block block)
    {
        m_Blocks.Remove(block);
        Destroy(block.gameObject);
    }

    Node GetNodeAtPosition(Vector2 position) => m_Nodes.FirstOrDefault(n => n.Position == position);

}

[Serializable]
public struct BlockType
{
    public int Value;
    public Color Color;
}

public enum GameState
{
    GenerateLevel,
    SpawningBlocks,
    WaitingInput,
    Moving,
    Win,
    Lose
}
