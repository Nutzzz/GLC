﻿using System.Linq;
using System.Collections.Generic;

using core.Platform;
using core.Game;

using Terminal.Gui;
using Terminal.Gui.Trees;

namespace glc.UI.Library
{
	public class CLibraryTab : TabView.Tab
    {
		private static View m_container;

		private static CPlatformTreePanel   m_platformPanel;
		private static CGamePanel		m_gamePanel;
		private static CGameInfoPanel	m_infoPanel;

		public CLibraryTab(List<CBasicPlatform> platforms)
			: base()
        {
			Text = "Library";

			// Construct the panels
			m_platformPanel = new CPlatformTreePanel(platforms, "Platforms", 0, 0, Dim.Percent(25), Dim.Fill(), true);

			List<GameObject> gameList = (platforms.Count > 0) ? CGameSQL.LoadPlatformGames(platforms[0].PrimaryKey).ToList() : new List<GameObject>();
			m_gamePanel	= new CGamePanel(gameList, "Games", Pos.Percent(25), 0, Dim.Fill(), Dim.Percent(60), true);
			m_infoPanel = new CGameInfoPanel("", Pos.Percent(25), Pos.Percent(60), Dim.Fill(), Dim.Fill());

			// Hook up the triggers
			// Event triggers for the list view
			m_platformPanel.ContainerView.SelectionChanged += PlatformListView_SelectedChanged;
			m_platformPanel.ContainerView.ObjectActivated += PlatformListView_ObjectActivated;

			m_gamePanel.ContainerView.OpenSelectedItem += GameListView_OpenSelectedItem;
			m_gamePanel.ContainerView.SelectedItemChanged += GameListView_SelectedChanged;

			// Container to store all frames
			m_container = new View()
			{
				X = 0,
				Y = 0, // for menu
				Width = Dim.Fill(0),
				Height = Dim.Fill(0),
				CanFocus = false,
			};
			m_container.Add(m_platformPanel.FrameView);
			m_container.Add(m_gamePanel.FrameView);
			m_container.Add(m_infoPanel.FrameView);

			View = m_container;

			View.KeyDown += KeyDownHandler;
		}

		/// <summary>
		/// Handle change in the platform list view
		/// </summary>
		/// <param name="e">The event argument</param>
		private static void PlatformListView_SelectedChanged(object sender, SelectionChangedEventArgs<IPlatformTreeNode> e)
        {
			var val = e.NewValue;
			if(val == null)
            {
				return;
            }

			if(val is PlatformRootNode)
            {
				PlatformRootNode node = (PlatformRootNode)val;
				m_gamePanel.ContentList = CGameSQL.LoadPlatformGames(node.ID).ToList();
				m_gamePanel.ContainerView.Source = new CGameDataSource(m_gamePanel.ContentList);
			}
			else if(val is PlatformTagNode)
            {
				PlatformTagNode node = (PlatformTagNode)val;
				if(node.ID < 0)
                {
					// TODO: handle special
                }
				else if(node.Name.ToLower() == "favourites") // TODO: change
                {
					m_gamePanel.ContentList = CGameSQL.LoadPlatformGames(node.ID, true).ToList();
				}
				else
                {
					m_gamePanel.ContentList = CGameSQL.LoadPlatformGames(node.ID, node.Name).ToList();
				}
				m_gamePanel.ContainerView.Source = new CGameDataSource(m_gamePanel.ContentList);
			}
			else
            {
				return;
            }
		}

		private static void PlatformListView_ObjectActivated(ObjectActivatedEventArgs<IPlatformTreeNode> obj)
		{
			if(obj.ActivatedObject is PlatformRootNode root)
			{
				if(root.IsExpanded)
				{
					m_platformPanel.ContainerView.Collapse(root);
				}
				else
                {
					m_platformPanel.ContainerView.Expand(root);
				}
				root.IsExpanded = !root.IsExpanded;
			}
			else if(obj.ActivatedObject is PlatformTagNode leaf)
            {
				m_gamePanel.ContainerView.SetFocus();
			}
		}

		/// <summary>
		/// Handle game selection event
		/// </summary>
		/// <param name="e">The event argument</param>
		private static void GameListView_OpenSelectedItem(ListViewItemEventArgs e)
		{
			if(m_gamePanel.ContentList.Count == 0)
            {
				return;
            }
			GameObject game = m_gamePanel.ContentList[m_gamePanel.ContainerView.SelectedItem];

			int width = 30;
			int height = 10;

			var buttons = new List<Button> ();
			var clicked = -1;

			var okeyButton = new Button ("Okay", is_default: true);
			okeyButton.Clicked += () =>
			{
				clicked = 0;
				Application.RequestStop();
			};
			buttons.Add(okeyButton);

			var closeButton = new Button ("Close", is_default: false);
			closeButton.Clicked += () =>
			{
				clicked = 1;
				Application.RequestStop();
			};
			buttons.Add(closeButton);

			// This tests dynamically adding buttons; ensuring the dialog resizes if needed and
			// the buttons are laid out correctly
			var dialog = new Dialog (game.Title, width, height, buttons.ToArray());

			Application.Run(dialog);
		}

		private static void GameListView_SelectedChanged(ListViewItemEventArgs e)
        {
			if(m_gamePanel.ContentList.Count == 0)
			{
				m_infoPanel.FrameView.RemoveAll();
				return;
			}
			GameObject game = m_gamePanel.ContentList[m_gamePanel.ContainerView.SelectedItem];
			m_infoPanel.SwitchGameInfo(game);
		}

		private static void KeyDownHandler(View.KeyEventEventArgs a)
		{
			//if (a.KeyEvent.Key == Key.Tab || a.KeyEvent.Key == Key.BackTab) {
			//	// BUGBUG: Work around Issue #434 by implementing our own TAB navigation
			//	if (_top.MostFocused == _categoryListView)
			//		_top.SetFocus (_rightPane);
			//	else
			//		_top.SetFocus (_leftPane);
			//}

			// Game search
			if(a.KeyEvent.Key == (Key.CtrlMask | Key.F))
            {
				GameSearch();
            }
		}

		private static void GameSearch()
        {
			string searchTerm = "";
			CEditStringDlg searchDlg = new CEditStringDlg("Game search", searchTerm);
			if(searchDlg.Run(ref searchTerm))
            {
				m_platformPanel.SetSearchResults(searchTerm);
            }
        }
	}
}