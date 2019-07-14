using Godot;
using System;
using System.Linq;
using System.Collections.Generic;


public class BattleUI : Node2D
{
	public static BattleUI singleton;

	BattleUI()
	{
		singleton = this;
	}

	[Export]
	private AudioStream soundCoinHover;

	[Export]
	private AudioStream soundCoinClick;

	[Export]
	private AudioStream soundDrawHover;

	[Export]
	private AudioStream soundDrawClick;
	
	public enum Opponent { Blob };

	private bool five = false;
	private bool battleMode = false;
	private bool uiVisible = false;
	private bool uiBuffer = false;

	// PLAYER STATS
	private int playerHPCap = 20;
	private int playerHP = 20;
	private int playerMPCap = 5;
	private int playerMP = 0;

	private const string BATTLE_SCENE = "res://Scenes/Battle.tscn";
	private const float ENEMY_X = 2f;
	private const float ENEMY_Y = 0.35f;
	private const float ENEMY_Z = 0f;

	public enum Suit { Spade, Club, Heart, Diamond };

	private struct CardStruct
	{
		public CardStruct(Suit suit, int number, int manaCost)
		{
			this.suit = suit;
			this.number = number;
			this.manaCost = manaCost;
		}
		
		public Suit suit;
		public int number;
		public int manaCost;
	}

	private List<CardStruct> cardStash = new List<CardStruct>()//;
	{
		new CardStruct(Suit.Spade, 2, 1),
		new CardStruct(Suit.Spade, 2, 1),
		new CardStruct(Suit.Spade, 2, 1),
		new CardStruct(Suit.Spade, 3, 2),
		new CardStruct(Suit.Spade, 3, 2),
		new CardStruct(Suit.Heart, 3, 3),
		new CardStruct(Suit.Heart, 3, 3),
		new CardStruct(Suit.Heart, 3, 2),
		new CardStruct(Suit.Club, 1, 1),
		new CardStruct(Suit.Club, 1, 1),
		new CardStruct(Suit.Club, 2, 2),
		new CardStruct(Suit.Diamond, 2, 3),
		new CardStruct(Suit.Diamond, 2, 3),
		new CardStruct(Suit.Diamond, 2, 3),
	};

	private List<CardStruct> cardStashCurrentBattle = new List<CardStruct>();

	private int numJokers = 1;
	private int numJokersCurrent = 1;

	// Hovers
	private bool hoverJoker = false;
	private bool hoverDraw = false;

	private List<Tuple<CardStruct, int>> hand = new List<Tuple<CardStruct, int>>();

	private HashSet<Card> instancedCards = new HashSet<Card>();

	private int handSize = 0;

	private HashSet<int> indexChecks = new HashSet<int>(){0, 1, 2};

	// Turn state
	private bool drawnThisTurn = false;
	private bool jokerThisTurn = false;

	// Refs
	private AnimationPlayer animPlayerJoker;
	private AnimationPlayer animPlayerDraw;
	private AnimationPlayer animPlayerUI;
	private AnimationPlayer animPlayerSixfive;
	private Timer timerSetup;
	private Timer timerUIBuffer;
	private Sprite sprJoker;
	private Sprite sprDraw;
	private Sprite sprFive;
	private Sprite sprSix;
	private Label jokerLabel;
	private Label hpLabel;
	private Label mpLabel;

	private PackedScene cardRef = GD.Load<PackedScene>("res://Instances/Battle/Card.tscn");
	private PackedScene battleNumberRef = GD.Load<PackedScene>("res://Instances/Battle/BattleNumber.tscn");

	private PackedScene enemyBlob = GD.Load<PackedScene>("res://Instances/Enemies/EnemyBlob.tscn");

	// ================================================================

	public static int PlayerHP { get => BattleUI.singleton.playerHP; set => BattleUI.singleton.playerHP = value; }
	public static int PlayerMP { get => BattleUI.singleton.playerMP; set => BattleUI.singleton.playerMP = value; }
	public static bool Five { get => BattleUI.singleton.five; }
	public static int HandSize { get => BattleUI.singleton.handSize; set => BattleUI.singleton.handSize = value; }
	//public static bool UiVisible { get => BattleUI.singleton.uiVisible; }

	// ================================================================
	
	public override void _Ready()
	{
		animPlayerJoker = GetNode<AnimationPlayer>("AnimationPlayerJoker");
		animPlayerDraw = GetNode<AnimationPlayer>("AnimationPlayerDraw");
		animPlayerSixfive = GetNode<AnimationPlayer>("AnimationPlayerSixfive");
		animPlayerUI = GetNode<AnimationPlayer>("AnimationPlayerUI");
		timerSetup = GetNode<Timer>("TimerBattleSetup");
		timerUIBuffer = GetNode<Timer>("TimerUIBuffer");
		sprJoker = GetNode<Sprite>("Joker");
		sprDraw = GetNode<Sprite>("DrawButton");
		sprFive = GetNode<Sprite>("Five");
		sprSix = GetNode<Sprite>("Six");
		jokerLabel = GetNode<Label>("JokerLabel");
		hpLabel = GetNode<Sprite>("Healthbar").GetNode<Label>("Label");
		mpLabel = GetNode<Sprite>("Manabar").GetNode<Label>("Label");
	}


	public override void _Process(float delta)
	{
		sprDraw.Visible = battleMode;
		sprFive.Visible = battleMode;
		sprSix.Visible = battleMode;

		sprDraw.Modulate = handSize < 3 && !drawnThisTurn ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, 0.4f);
		sprJoker.Modulate = !jokerThisTurn ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, 0.4f);

		// Clicks
		if (Input.IsActionJustPressed("click_left") && battleMode)
		{
			if (hoverJoker) // Joker
			{
				Controller.PlaySoundBurst(soundCoinClick);
				animPlayerJoker.Play("Flip Joker");
				SwapDimension();
				jokerThisTurn = true;
				numJokersCurrent--;
				hoverJoker = false;
			}

			if (hoverDraw) // Draw
			{
				Controller.PlaySoundBurst(soundDrawClick);
				int index = Mathf.RoundToInt((float)GD.RandRange(0, cardStashCurrentBattle.Count - 1));

				int thisHandIndex;

				// I'm really sorry about this
				HashSet<int> found = new HashSet<int>();
				foreach (var card in hand)
					found.Add(card.Item2);

				found.SymmetricExceptWith(indexChecks);
				thisHandIndex = found.Min();

				hand.Add(new Tuple<CardStruct, int>(cardStashCurrentBattle[index], thisHandIndex));
				handSize++;
				CardStruct thisCard = new CardStruct(
					cardStashCurrentBattle[index].suit,
					cardStashCurrentBattle[index].number,
					cardStashCurrentBattle[index].manaCost
				);

				cardStashCurrentBattle.RemoveAt(index);
				InstantiateCard(thisCard, thisHandIndex);
				drawnThisTurn = true;
				hoverDraw = false;
			}
		}

		// UI update stuff
		jokerLabel.Text = $"x {numJokersCurrent.ToString()}";
		hpLabel.Text = playerHP.ToString();
		mpLabel.Text = $"{playerMP.ToString()}/{playerMPCap}";

		if (Input.IsActionJustPressed("debug_1"))
			BattleStart(Opponent.Blob);

		if (Input.IsActionJustPressed("debug_2"))
			DeallocateCards();

		if (Input.IsActionJustPressed("ui_show") && !battleMode && Player.State == Player.ST.Move)
			ShowUI(!uiVisible);
	}

	// ================================================================

	public static void BattleStart(Opponent opponent)
	{
		Player.State = Player.ST.Battle;
		PlayerMP = Mathf.FloorToInt((float)BattleUI.singleton.playerMPCap / 2f);
		Controller.GotoScene(BATTLE_SCENE);
		Player.singleton.Translation = new Vector3(0, 0.35f, 0);
		Player.Vel = Vector3.Zero;
		BattleUI.singleton.battleMode = true;

		switch (opponent)
		{
			case Opponent.Blob:
			{
				var e = (EnemyBlob)BattleUI.singleton.enemyBlob.Instance();
				e.Translation = new Vector3(ENEMY_X, ENEMY_Y, ENEMY_Z);
				BattleUI.singleton.GetTree().GetRoot().AddChild(e);
				break;
			}
		}

		BattleUI.singleton.timerSetup.Start();
	}


	public static void ShowUI(bool show)
	{
		if (!BattleUI.singleton.uiBuffer && BattleUI.singleton.uiVisible != show)
		{
			BattleUI.singleton.animPlayerUI.Play(show ? "Show UI" : "Hide UI");
			BattleUI.singleton.uiVisible = show;

			BattleUI.singleton.uiBuffer = true;
			BattleUI.singleton.timerUIBuffer.Start();
		}
	}


	public static void SwapDimension()
	{
		BattleUI.singleton.five ^= true;
		BattleUI.singleton.animPlayerSixfive.Play(BattleUI.singleton.five ? "Switch to Five" : "Switch to Six");
	}


	public static void RemoveCardFromHand(int index)
	{
		foreach (var card in BattleUI.singleton.hand)
		{
			if (card.Item2 == index)
			{
				BattleUI.singleton.hand.Remove(card);
				HandSize--;
				break;
			}
		}
	}


	public static void ClickCard(Suit suit, int number, int cost)
	{
		foreach (var card in BattleUI.singleton.instancedCards)
		{
			if (!card.Selected)
				card.Discarded = true;
		}

		PlayerMP -= cost;

		ShowUI(false);

		switch (suit)
		{
			case Suit.Spade: // Attack
			{
				break;
			}

			case Suit.Club: // Defend
			{
				break;
			}

			case Suit.Heart: // Heal
			{
				break;
			}

			case Suit.Diamond: // Wild
			{
				break;
			}
		}
	}

	// ================================================================

	private void BattleSetup()
	{
		MainCamera.singleton.Current = false;
		ShowUI(true);
		cardStashCurrentBattle = new List<CardStruct>(cardStash);
		RandomizeHand();
		InstantiateCardsInHand();
	}


	private void RandomizeHand()
	{
		hand.Clear();
		handSize = 3;
		
		for (int i = 0; i < 3; i++)
		{
			int index = Mathf.RoundToInt((float)GD.RandRange(0, cardStashCurrentBattle.Count - 1));
			hand.Add(new Tuple<CardStruct, int>(cardStashCurrentBattle[index], i));
			cardStashCurrentBattle.RemoveAt(index);
		}
	}


	private void InstantiateCard(CardStruct card, int index)
	{
		var cardInst = (Card)cardRef.Instance();
		cardInst.SetSuit((Card.Suit)(int)card.suit);
		cardInst.SetNumber(card.number);
		cardInst.SetCost(card.manaCost);
		cardInst.HandIndex = index;
		cardInst.Position = new Vector2(360 + 212 * index, 900);
		instancedCards.Add(cardInst);
		GetTree().GetRoot().AddChild(cardInst);
	}


	private void InstantiateCardsInHand()
	{
		for (int i = 0; i < hand.Count; i++)
		{
			var cardInst = (Card)cardRef.Instance();
			cardInst.SetSuit((Card.Suit)(int)hand[i].Item1.suit);
			cardInst.SetNumber(hand[i].Item1.number);
			cardInst.SetCost(hand[i].Item1.manaCost);
			cardInst.HandIndex = hand[i].Item2;
			//cardInst.Position = new Vector2(360 + 212 * i, 695);
			cardInst.Position = new Vector2(360 + 212 * i, 900);
			instancedCards.Add(cardInst);
			GetTree().GetRoot().AddChild(cardInst);
		}
	}


	private void DeallocateCards()
	{
		foreach (var card in instancedCards)
		{
			card.QueueFree();
		}
	}


	private void ResetBuffer()
	{
		uiBuffer = false;
	}


	private void JokerHoverStart()
	{
		if (battleMode && !jokerThisTurn && uiVisible && numJokersCurrent > 0)
		{
			Controller.PlaySoundBurst(soundCoinHover);
			animPlayerJoker.Play("Joker Hover");
			BattleUI.singleton.hoverJoker = true;
		}
	}


	private void JokerHoverEnd()
	{
		if (battleMode && !jokerThisTurn && uiVisible && numJokersCurrent > 0)
			BattleUI.singleton.hoverJoker = false;
	}


	private void DrawHoverStart()
	{
		if (battleMode && !drawnThisTurn && uiVisible && handSize < 3)
		{
			Controller.PlaySoundBurst(soundDrawHover);
			animPlayerDraw.Play("Draw Hover");
			BattleUI.singleton.hoverDraw = true;
		}
	}

	private void DrawHoverEnd()
	{
		if (battleMode && !drawnThisTurn && uiVisible && handSize < 3)
			BattleUI.singleton.hoverDraw = false;
	}
}
