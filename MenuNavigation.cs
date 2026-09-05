using System;
using System.Collections.Generic;
using UnityEngine;
using rowemod.Challenges;
using rowemod.Mods;
using rowemod.Utils;
using static rowemod.Config;
using static rowemod.Mods.Misc;
using static rowemod.Utils.Memory;

namespace rowemod
{
    /// <summary>
    /// Task-based navigation and page composition for the RoweMod menu. Existing feature editors
    /// keep their native ownership; this layer only decides where they appear and when their
    /// existing tab lifecycle hooks run.
    /// </summary>
    public static partial class Menu
    {
        private static int _selectedDanceIndex;

        private enum MenuArea
        {
            Ride,
            Tricks,
            Customize,
            Camera,
            World,
            Graphics,
            Advanced
        }

        private enum MenuPage
        {
            RideHandling,
            RidePrediction,
            RideSafety,
            RideVehicleTuning,
            TrickMapping,
            GrindPoses,
            BikeParts,
            BikeFit,
            BikeMaterials,
            RiderAppearance,
            RiderEmotes,
            RiderTools,
            BikeStudio,
            CameraGameplay,
            CameraLens,
            CameraFocus,
            CameraFraming,
            CameraLight,
            CameraPresets,
            WorldDropper,
            WorldMarker,
            WorldDrone,
            WorldVehicleSpawns,
            GraphicsPerformance,
            GraphicsEnvironment,
            GraphicsSceneLights,
            AdvancedStartup,
            AdvancedInterface,
            AdvancedMultiplayer,
            AdvancedDeveloper
        }

        private sealed class PageDefinition
        {
            public readonly MenuPage Page;
            public readonly MenuArea Area;
            public readonly string Label;
            public readonly string Description;
            public readonly Tab LegacyTab;
            public readonly string SearchTerms;
            public readonly string ResetScope;

            public PageDefinition(
                MenuPage page,
                MenuArea area,
                string label,
                string description,
                Tab legacyTab,
                string searchTerms,
                string resetScope)
            {
                Page = page;
                Area = area;
                Label = label;
                Description = description;
                LegacyTab = legacyTab;
                SearchTerms = searchTerms;
                ResetScope = resetScope;
            }
        }

        private sealed class AreaDefinition
        {
            public readonly MenuArea Area;
            public readonly string Label;
            public readonly MenuPage[] Pages;

            public AreaDefinition(MenuArea area, string label, params MenuPage[] pages)
            {
                Area = area;
                Label = label;
                Pages = pages;
            }
        }

        private static readonly PageDefinition[] NavigationPages =
        {
            new PageDefinition(MenuPage.RideHandling, MenuArea.Ride, "Handling",
                "Core riding assists, pump, spins, manuals, and bike response.", Tab.Physics,
                "physics spin assist grind align drift gravity hop pump manual nose manual steering damping handling", "Ride"),
            new PageDefinition(MenuPage.RidePrediction, MenuArea.Ride, "Prediction Lab",
                "Inspect landing arcs and tune the game's flight, coping, and lip prediction systems.", Tab.Physics,
                "prediction landing arc trajectory rays flight augment corner detector coping lip magnet ragdoll rigidbody joints muscles", "Prediction Lab"),
            new PageDefinition(MenuPage.RideSafety, MenuArea.Ride, "Safety",
                "Bail and injury behavior that changes how mistakes are handled.", Tab.Misc,
                "no bail never bail bone breaking injury bone strength safety", "Safety"),
            new PageDefinition(MenuPage.RideVehicleTuning, MenuArea.Ride, "Vehicle Tuning",
                "Everyday speed controls, per-vehicle motors, presets, and the advanced inspector.", Tab.Physics,
                "vehicle tuning motor speed force acceleration spin stops damping preset inspector proto bmx finish spins facing forward landing assist", "Vehicle Tuning"),

            new PageDefinition(MenuPage.TrickMapping, MenuArea.Tricks, "Trick Mapping",
                "Bind and search all eight directions in each trick set.", Tab.Tricks,
                "tricks bindings input map direction up down left right animation bind", "Trick Mapping"),
            new PageDefinition(MenuPage.GrindPoses, MenuArea.Tricks, "Grind Poses",
                "Tune grind transitions, rider offsets, bike controls, sparks, and pose presets.", Tab.Grinds,
                "grind pose peg sparks ching volume pitch crank tooth smith feeble luc-e", "Grind Poses"),

            new PageDefinition(MenuPage.BikeParts, MenuArea.Customize, "Bike Parts",
                "Swap frames, bars, stems, forks, wheels, and other loaded parts.", Tab.Bike,
                "bike parts frame bars stem fork wheels swap model", "Bike"),
            new PageDefinition(MenuPage.BikeFit, MenuArea.Customize, "Bike Fit",
                "Adjust seat, bars, wheel scale, peg visibility, and fitted offsets.", Tab.Bike,
                "bike fit seat height tilt bars pitch scale wheels pegs adjustment", "Bike"),
            new PageDefinition(MenuPage.BikeMaterials, MenuArea.Customize, "Materials",
                "Apply and save materials for individual bike parts.", Tab.BikeMaterials,
                "bike material paint color texture bundle preset", "Bike Materials"),
            new PageDefinition(MenuPage.RiderAppearance, MenuArea.Customize, "Rider",
                "Choose rider models, clothing materials, visibility, and appearance presets.", Tab.Character,
                "rider character clothing model shirt pants shoes hair hat appearance", "Rider Appearance"),
            new PageDefinition(MenuPage.RiderEmotes, MenuArea.Customize, "Emotes",
                "Play the game's stock emotes, added poses, and imported humanoid clips.", Tab.Character,
                "rider emote pose sitting sit dance gun fingers party animation", "Emotes"),
            new PageDefinition(MenuPage.RiderTools, MenuArea.Customize, "Stance & Look",
                "Bike-only stance, manual and nose-manual foot IK, and rider head tracking.", Tab.RiderTools,
                "rider tools stance goofy regular opposite manny manual nosey nose manual foot ik target variant head tracking look", "Rider Tools"),
            new PageDefinition(MenuPage.BikeStudio, MenuArea.Customize, "Bike Studio",
                "Place a clean visual bike copy for screenshots and precise posing.", Tab.BikePoser,
                "bike poser studio snapshot photo gizmo transform rotate place", "Bike Studio"),

            new PageDefinition(MenuPage.CameraGameplay, MenuArea.Camera, "Gameplay",
                "Gameplay camera shortcuts and replay collision behavior.", Tab.Camera,
                "camera gameplay left stick offset collision collider freecam", "Gameplay Camera"),
            new PageDefinition(MenuPage.CameraLens, MenuArea.Camera, "Lens",
                "Replay FOV, tilt, fisheye optics, vignette, and lens character.", Tab.Replay,
                "camera replay lens fov fisheye vx1000 mk1 vignette tilt optical zoom crop", "Replay Camera"),
            new PageDefinition(MenuPage.CameraFocus, MenuArea.Camera, "Focus",
                "Native replay depth-of-field and focus keyframes.", Tab.Replay,
                "camera replay focus depth of field dof aperture near far", "Replay Camera"),
            new PageDefinition(MenuPage.CameraFraming, MenuArea.Camera, "Framing",
                "Capture-safe aspect mattes and framing keyframes.", Tab.Replay,
                "camera replay frame framing aspect matte crop letterbox vertical video", "Replay Camera"),
            new PageDefinition(MenuPage.CameraLight, MenuArea.Camera, "Replay Light",
                "A replay-only camera light with keyframed intensity and placement.", Tab.Replay,
                "camera replay light intensity temperature range shadows", "Replay Camera"),
            new PageDefinition(MenuPage.CameraPresets, MenuArea.Camera, "Presets",
                "Save and load complete replay camera looks.", Tab.Replay,
                "camera replay preset save load lens dof frame light", "Replay Camera"),

            new PageDefinition(MenuPage.WorldDropper, MenuArea.World, "Object Dropper",
                "Browse, place, select, and transform bundled world objects.", Tab.Dropper,
                "world object dropper place spawn prop transform", "Object Dropper"),
            new PageDefinition(MenuPage.WorldMarker, MenuArea.World, "Session Marker",
                "Choose the model used for the current session marker.", Tab.Marker,
                "world marker session respawn prefab", "Session Marker"),
            new PageDefinition(MenuPage.WorldDrone, MenuArea.World, "Drone",
                "Control drone visuals, sound, collision, and mass.", Tab.Misc,
                "world drone body sound emitter collider mass", "Drone"),
            new PageDefinition(MenuPage.WorldVehicleSpawns, MenuArea.World, "Vehicle Spawns",
                "Spawn RoweMod utility vehicles in front of the player.", Tab.Misc,
                "world spawn drift car trike vehicle", null),

            new PageDefinition(MenuPage.GraphicsPerformance, MenuArea.Graphics, "Performance",
                "Choose a quality preset or customize expensive HDRP features.", Tab.Graphics,
                "graphics performance fps potato low balanced quality shadow lod terrain texture antialiasing effects", "Graphics"),
            new PageDefinition(MenuPage.GraphicsEnvironment, MenuArea.Graphics, "Environment",
                "Choose an HDRI sky and save exposure independently for each scene.", Tab.Graphics,
                "graphics hdri sky night sunset clear exposure environment brightness rotation", "Graphics"),
            new PageDefinition(MenuPage.GraphicsSceneLights, MenuArea.Graphics, "Scene Lights",
                "Adjust live scene-light intensity from a cached map inventory.", Tab.Graphics,
                "graphics scene lights intensity brightness map", "Graphics"),

            new PageDefinition(MenuPage.AdvancedStartup, MenuArea.Advanced, "Startup",
                "General behavior used when entering the game.", Tab.Misc,
                "advanced startup intro skip main menu", "Startup"),
            new PageDefinition(MenuPage.AdvancedInterface, MenuArea.Advanced, "Interface",
                "Customize the RoweMod menu appearance.", Tab.Misc,
                "advanced interface ui menu accent color theme", "Interface"),
            new PageDefinition(MenuPage.AdvancedMultiplayer, MenuArea.Advanced, "Multiplayer",
                "Player labels and available multiplayer challenge controls.", Tab.Multiplayer,
                "advanced multiplayer mp online player labels username challenge", "Multiplayer"),
            new PageDefinition(MenuPage.AdvancedDeveloper, MenuArea.Advanced, "Developer",
                "Diagnostics and tools intended for troubleshooting.", Tab.Debug,
                "advanced developer debug center mass logs map loader diagnostics", "Developer Tools")
        };

        private static readonly AreaDefinition[] NavigationAreas =
        {
            new AreaDefinition(MenuArea.Ride, "Ride",
                MenuPage.RideHandling, MenuPage.RidePrediction, MenuPage.RideSafety, MenuPage.RideVehicleTuning),
            new AreaDefinition(MenuArea.Tricks, "Tricks",
                MenuPage.TrickMapping, MenuPage.GrindPoses),
            new AreaDefinition(MenuArea.Customize, "Customize",
                MenuPage.BikeParts, MenuPage.BikeFit, MenuPage.BikeMaterials,
                MenuPage.RiderAppearance, MenuPage.RiderEmotes, MenuPage.RiderTools, MenuPage.BikeStudio),
            new AreaDefinition(MenuArea.Camera, "Camera",
                MenuPage.CameraGameplay, MenuPage.CameraLens, MenuPage.CameraFocus,
                MenuPage.CameraFraming, MenuPage.CameraLight, MenuPage.CameraPresets),
            new AreaDefinition(MenuArea.World, "World",
                MenuPage.WorldDropper, MenuPage.WorldMarker, MenuPage.WorldDrone, MenuPage.WorldVehicleSpawns),
            new AreaDefinition(MenuArea.Graphics, "Graphics",
                MenuPage.GraphicsPerformance, MenuPage.GraphicsEnvironment, MenuPage.GraphicsSceneLights),
            new AreaDefinition(MenuArea.Advanced, "Advanced",
                MenuPage.AdvancedStartup, MenuPage.AdvancedInterface,
                MenuPage.AdvancedMultiplayer, MenuPage.AdvancedDeveloper)
        };

        private static readonly MenuPage[] LastPageByArea =
        {
            MenuPage.RideHandling,
            MenuPage.TrickMapping,
            MenuPage.BikeParts,
            MenuPage.CameraGameplay,
            MenuPage.WorldDropper,
            MenuPage.GraphicsPerformance,
            MenuPage.AdvancedStartup
        };

        private static MenuArea _selectedArea = MenuArea.Ride;
        private static MenuPage _selectedPage = MenuPage.RideHandling;
        private static bool _navigationInitialized;
        private static string _menuSearch = string.Empty;
        private static bool _resetConfirmationActive;
        private static MenuPage _resetConfirmationPage;
        private static float _resetConfirmationExpires;
        private static Light[] _sceneLightCache = Array.Empty<Light>();
        private static float NavigationHintHeight => 58f * UiScale;

        private static void RefreshSceneLightCache()
        {
            _sceneLightCache = UnityEngine.Object.FindObjectsOfType<Light>() ?? Array.Empty<Light>();
            CacheLightDefaults(_sceneLightCache);
        }

        public static void NotifySceneInitialized()
        {
            _sceneLightCache = Array.Empty<Light>();
            _cachedLightIntensityById.Clear();
        }

        private static float GetResponsiveSidebarWidth()
        {
            return windowRect.width < 800f * UiScale ? 150f * UiScale : UiSidebarWidth;
        }

        private static void EnsureNavigationInitialized()
        {
            if (_navigationInitialized)
                return;

            PageDefinition initial = FindFirstPageForLegacyTab(currentTab) ?? FindPage(MenuPage.RideHandling);
            _selectedPage = initial.Page;
            _selectedArea = initial.Area;
            LastPageByArea[(int)_selectedArea] = _selectedPage;
            _navigationInitialized = true;
        }

        private static PageDefinition FindPage(MenuPage page)
        {
            for (int i = 0; i < NavigationPages.Length; i++)
            {
                if (NavigationPages[i].Page == page)
                    return NavigationPages[i];
            }

            return NavigationPages[0];
        }

        private static AreaDefinition FindArea(MenuArea area)
        {
            for (int i = 0; i < NavigationAreas.Length; i++)
            {
                if (NavigationAreas[i].Area == area)
                    return NavigationAreas[i];
            }

            return NavigationAreas[0];
        }

        private static PageDefinition FindFirstPageForLegacyTab(Tab tab)
        {
            for (int i = 0; i < NavigationPages.Length; i++)
            {
                if (NavigationPages[i].LegacyTab == tab)
                    return NavigationPages[i];
            }

            return null;
        }

        private static string GetAreaLabel(MenuArea area)
        {
            return FindArea(area).Label;
        }

        private static string GetCurrentPageLabel()
        {
            EnsureNavigationInitialized();
            return string.IsNullOrWhiteSpace(_menuSearch) ? FindPage(_selectedPage).Label : "Find Settings";
        }

        private static int GetSubNavigationColumns(float width, int pageCount)
        {
            if (pageCount <= 1)
                return 1;

            float minimumButtonWidth = 108f * UiScale;
            int columns = Mathf.FloorToInt((Mathf.Max(1f, width) + UiTabSpacing) /
                                           (minimumButtonWidth + UiTabSpacing));
            return Mathf.Clamp(columns, 1, pageCount);
        }

        private static int GetSubNavigationRows()
        {
            if (!string.IsNullOrWhiteSpace(_menuSearch))
                return 0;

            EnsureNavigationInitialized();
            AreaDefinition area = FindArea(_selectedArea);
            int columns = GetSubNavigationColumns(GetContentWidth() - (UiInnerPadding * 2f), area.Pages.Length);
            return Mathf.CeilToInt(area.Pages.Length / (float)columns);
        }

        private static float GetNavigationHeaderHeight()
        {
            float headerBodyHeight = 112f * UiScale;
            int rows = GetSubNavigationRows();
            return headerBodyHeight + (rows * (44f * UiScale));
        }

        private static void DrawNavigationHeader()
        {
            EnsureNavigationInitialized();
            PageDefinition page = FindPage(_selectedPage);
            bool searching = !string.IsNullOrWhiteSpace(_menuSearch);
            float headerHeight = GetNavigationHeaderHeight();
            Rect headerRect = new Rect(GetContentX(), UiTitleBarHeight + UiOuterPadding, GetContentWidth(), headerHeight);
            GUI.Box(headerRect, GUIContent.none, tabBarStyle);

            float resetWidth = page.ResetScope == null || searching ? 0f : 156f * UiScale;
            float textWidth = headerRect.width - (UiInnerPadding * 2f) - (resetWidth > 0f ? resetWidth + UiInnerPadding : 0f);
            Rect eyebrowRect = new Rect(headerRect.x + UiInnerPadding, headerRect.y + (10f * UiScale),
                textWidth, 17f * UiScale);
            GUI.Label(eyebrowRect, searching ? "SEARCH" : GetAreaLabel(_selectedArea).ToUpperInvariant(), pageEyebrowStyle);
            Rect titleRect = new Rect(headerRect.x + UiInnerPadding, headerRect.y + (27f * UiScale),
                textWidth, 38f * UiScale);
            GUI.Label(titleRect, searching ? "Find Settings" : page.Label, pageTitleStyle ?? sectionHeaderStyle);
            Rect detailRect = new Rect(headerRect.x + UiInnerPadding, headerRect.y + (67f * UiScale),
                textWidth, 38f * UiScale);
            GUI.Label(detailRect,
                searching ? "Search by player-facing names, technical names, or common phrases." : page.Description,
                UiMutedWrappedStyle);

            if (resetWidth > 0f)
            {
                DrawPageResetButton(page, headerRect, resetWidth);
                Rect scopeRect = new Rect(headerRect.xMax - UiInnerPadding - resetWidth,
                    headerRect.y + (70f * UiScale), resetWidth, 20f * UiScale);
                GUI.Label(scopeRect, $"Scope: {page.ResetScope}", subtleLabelStyle);
            }

            if (!searching)
                DrawSubNavigation(headerRect, FindArea(_selectedArea));
        }

        private static void DrawPageResetButton(PageDefinition page, Rect headerRect, float width)
        {
            if (_resetConfirmationActive && Time.unscaledTime > _resetConfirmationExpires)
                _resetConfirmationActive = false;

            bool confirming = _resetConfirmationActive && _resetConfirmationPage == page.Page;
            Rect buttonRect = new Rect(headerRect.xMax - UiInnerPadding - width,
                headerRect.y + (24f * UiScale), width, 40f * UiScale);
            string label = confirming ? "Confirm Restore" : "Restore Defaults";
            GUIStyle style = confirming
                ? redButtonStyle ?? highQualityButtonStyle
                : highQualityButtonStyle;

            if (!ControllerButton(buttonRect, "page_restore_defaults", label, style))
                return;

            if (!confirming)
            {
                _resetConfirmationActive = true;
                _resetConfirmationPage = page.Page;
                _resetConfirmationExpires = Time.unscaledTime + 4f;
                return;
            }

            _resetConfirmationActive = false;
            ResetSelectedPage(page.Page);
        }

        private static void DrawSubNavigation(Rect headerRect, AreaDefinition area)
        {
            float left = headerRect.x + UiInnerPadding;
            float availableWidth = headerRect.width - (UiInnerPadding * 2f);
            int columns = GetSubNavigationColumns(availableWidth, area.Pages.Length);
            float tabGap = 4f * UiScale;
            float buttonWidth = (availableWidth - ((columns - 1) * tabGap)) / columns;
            float y = headerRect.y + (112f * UiScale);

            // A visible rail makes this read as page navigation instead of a row of links.
            DrawSolidColorRect(
                new Rect(left, y - (1f * UiScale), availableWidth, 1f * UiScale),
                uiBorderColor);

            for (int i = 0; i < area.Pages.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                PageDefinition page = FindPage(area.Pages[i]);
                Rect buttonRect = new Rect(
                    left + column * (buttonWidth + tabGap),
                    y + row * (44f * UiScale),
                    buttonWidth,
                    42f * UiScale);
                bool selected = page.Page == _selectedPage;
                if (GUI.Button(buttonRect, page.Label, selected ? subTabActiveButtonStyle : subTabButtonStyle))
                    SelectPage(page.Page);

                if (selected)
                {
                    Rect indicatorRect = new Rect(
                        buttonRect.x + (10f * UiScale),
                        buttonRect.y + (1f * UiScale),
                        Mathf.Max(8f * UiScale, buttonRect.width - (20f * UiScale)),
                        3f * UiScale);
                    DrawSolidColorRect(indicatorRect, uiAccentColor);
                }
            }
        }

        private static void DrawAreaNavigation()
        {
            EnsureNavigationInitialized();
            float sidebarWidth = GetResponsiveSidebarWidth();
            float searchY = UiTitleBarHeight + (40f * UiScale);
            float searchWidth = sidebarWidth - (UiOuterPadding * 2f);
            float clearWidth = string.IsNullOrEmpty(_menuSearch) ? 0f : 32f * UiScale;
            Rect searchRect = new Rect(UiOuterPadding, searchY, searchWidth - clearWidth, 36f * UiScale);
            string previousSearch = _menuSearch;
            bool wasSearching = !string.IsNullOrWhiteSpace(previousSearch);
            _menuSearch = GUI.TextField(searchRect, _menuSearch ?? string.Empty, UiSearchFieldStyle);
            if (string.IsNullOrEmpty(_menuSearch))
                GUI.Label(new Rect(searchRect.x + (10f * UiScale), searchRect.y + (4f * UiScale),
                        searchRect.width - (16f * UiScale), searchRect.height - (8f * UiScale)),
                    "Find settings...", subtleLabelStyle);

            if (clearWidth > 0f)
            {
                Rect clearRect = new Rect(searchRect.xMax, searchRect.y, clearWidth, searchRect.height);
                if (GUI.Button(clearRect, "×", tabButtonStyle))
                    _menuSearch = string.Empty;
            }

            bool isSearching = !string.IsNullOrWhiteSpace(_menuSearch);
            if (!string.Equals(previousSearch, _menuSearch, StringComparison.Ordinal))
            {
                scrollOffset = 0f;
                if (!wasSearching && isSearching)
                    SetCurrentTab(Tab.Misc);
                else if (wasSearching && !isSearching)
                    SetCurrentTab(FindPage(_selectedPage).LegacyTab);
            }

            float navTop = UiTitleBarHeight + UiLogoAreaHeight + UiOuterPadding;
            float navWidth = sidebarWidth - (UiOuterPadding * 2f);
            float navHeight = Mathf.Max(120f,
                windowRect.height - navTop - UiOuterPadding - NavigationHintHeight - (4f * UiScale));
            Rect navRect = new Rect(UiOuterPadding, navTop, navWidth, navHeight);
            float totalHeight = (NavigationAreas.Length * UiNavButtonHeight) +
                                ((NavigationAreas.Length - 1) * UiTabSpacing);
            bool overflow = totalHeight > navRect.height;
            float buttonWidth = overflow ? navWidth - (14f * UiScale) : navWidth;
            Rect viewRect = new Rect(0f, 0f, buttonWidth, Mathf.Max(totalHeight, navRect.height));

            if (Event.current.type == EventType.ScrollWheel && navRect.Contains(Event.current.mousePosition))
            {
                _tabScrollPosition.y += Event.current.delta.y * (22f * UiScale);
                Event.current.Use();
            }

            _tabScrollPosition = GUI.BeginScrollView(navRect, _tabScrollPosition, viewRect, false, overflow);
            _tabScrollPosition.y = Mathf.Clamp(_tabScrollPosition.y, 0f,
                Mathf.Max(0f, viewRect.height - navRect.height));

            float y = 0f;
            for (int i = 0; i < NavigationAreas.Length; i++)
            {
                AreaDefinition area = NavigationAreas[i];
                Rect buttonRect = new Rect(0f, y, buttonWidth, UiNavButtonHeight);
                bool selected = area.Area == _selectedArea && string.IsNullOrWhiteSpace(_menuSearch);
                if (GUI.Button(buttonRect, area.Label, selected ? activeTabButtonStyle : tabButtonStyle))
                {
                    _menuSearch = string.Empty;
                    SelectArea(area.Area);
                }

                if (selected)
                {
                    Rect indicatorRect = new Rect(buttonRect.x + (2f * UiScale),
                        buttonRect.y + (7f * UiScale),
                        4f * UiScale,
                        buttonRect.height - (14f * UiScale));
                    if (tabIndicatorTexture != null)
                        GUI.DrawTexture(indicatorRect, tabIndicatorTexture);
                    else
                        DrawSolidColorRect(indicatorRect, uiAccentColor);
                }

                y += UiNavButtonHeight + UiTabSpacing;
            }

            GUI.EndScrollView();

            float hintY = windowRect.height - UiOuterPadding - NavigationHintHeight;
            GUI.Label(new Rect(UiOuterPadding, hintY, navWidth, 18f * UiScale),
                "D-pad Navigate  A Select", subtleLabelStyle);
            GUI.Label(new Rect(UiOuterPadding, hintY + (18f * UiScale), navWidth, 18f * UiScale),
                "B Close  RS Scroll", subtleLabelStyle);
            GUI.Label(new Rect(UiOuterPadding, hintY + (36f * UiScale), navWidth, 18f * UiScale),
                "LB/RB Area  LT/RT Page", subtleLabelStyle);
        }

        private static void SelectArea(MenuArea area)
        {
            EnsureNavigationInitialized();
            LastPageByArea[(int)_selectedArea] = _selectedPage;
            MenuPage requestedPage = LastPageByArea[(int)area];
            PageDefinition page = FindPage(requestedPage);
            if (page.Area != area)
                page = FindPage(FindArea(area).Pages[0]);

            SelectPage(page.Page);
        }

        private static void EnsureSelectedAreaVisible()
        {
            EnsureNavigationInitialized();
            int index = (int)_selectedArea;
            float navTop = UiTitleBarHeight + UiLogoAreaHeight + UiOuterPadding;
            float navHeight = Mathf.Max(120f,
                windowRect.height - navTop - UiOuterPadding - NavigationHintHeight - (4f * UiScale));
            float totalHeight = (NavigationAreas.Length * UiNavButtonHeight) +
                                ((NavigationAreas.Length - 1) * UiTabSpacing);
            float maxScroll = Mathf.Max(0f, totalHeight - navHeight);
            float itemTop = index * (UiNavButtonHeight + UiTabSpacing);
            float itemBottom = itemTop + UiNavButtonHeight;

            if (itemTop < _tabScrollPosition.y)
                _tabScrollPosition.y = itemTop;
            else if (itemBottom > _tabScrollPosition.y + navHeight)
                _tabScrollPosition.y = itemBottom - navHeight;

            _tabScrollPosition.y = Mathf.Clamp(_tabScrollPosition.y, 0f, maxScroll);
        }

        private static void SelectArea(int direction)
        {
            EnsureNavigationInitialized();
            int index = (int)_selectedArea;
            int nextIndex = (index + direction + NavigationAreas.Length) % NavigationAreas.Length;
            _menuSearch = string.Empty;
            SelectArea(NavigationAreas[nextIndex].Area);
            Log.Msg($"[ControllerMenu] Area {index}->{nextIndex}: {NavigationAreas[nextIndex].Label}.");
        }

        public static void SelectNextPage()
        {
            SelectPageByOffset(1);
        }

        public static void SelectPreviousPage()
        {
            SelectPageByOffset(-1);
        }

        private static void SelectPageByOffset(int direction)
        {
            EnsureNavigationInitialized();
            AreaDefinition area = FindArea(_selectedArea);
            int index = 0;
            for (int i = 0; i < area.Pages.Length; i++)
            {
                if (area.Pages[i] == _selectedPage)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + direction + area.Pages.Length) % area.Pages.Length;
            _menuSearch = string.Empty;
            SelectPage(area.Pages[next]);
        }

        private static void SelectPage(MenuPage page)
        {
            EnsureNavigationInitialized();
            PageDefinition definition = FindPage(page);
            _selectedArea = definition.Area;
            _selectedPage = definition.Page;
            LastPageByArea[(int)_selectedArea] = _selectedPage;
            _resetConfirmationActive = false;
            scrollOffset = 0f;
            scrollViewHeight = 10000f;
            ResetControllerNavigation();
            SetCurrentTab(definition.LegacyTab);

            if (definition.Page == MenuPage.GraphicsSceneLights)
                RefreshSceneLightCache();
        }

        private static bool IsTrickMappingContentActive()
        {
            return string.IsNullOrWhiteSpace(_menuSearch) && _selectedPage == MenuPage.TrickMapping;
        }

        private static void DrawTabContent()
        {
            EnsureNavigationInitialized();
            if (!string.IsNullOrWhiteSpace(_menuSearch))
            {
                DrawSettingsSearchResults();
                return;
            }

            switch (_selectedPage)
            {
                case MenuPage.RideHandling:
                    DrawRideHandlingPage();
                    break;
                case MenuPage.RidePrediction:
                    DrawPredictionLabPage();
                    break;
                case MenuPage.RideSafety:
                    DrawRideSafetyPage();
                    break;
                case MenuPage.RideVehicleTuning:
                    DrawRideVehicleTuningPage();
                    break;
                case MenuPage.TrickMapping:
                    TrickMods.DrawTrickMenuPro();
                    break;
                case MenuPage.GrindPoses:
                    GrindPoseEditor.DrawGrindPoseTab();
                    break;
                case MenuPage.BikeParts:
                    PartTweaker.DrawPartSelectorUI(GUILayout.ExpandWidth(true), GUILayout.Height(GetContentPaneHeight(24f)));
                    break;
                case MenuPage.BikeFit:
                    PartTweaker.DrawPartTweaker(GUILayout.ExpandWidth(true), GUILayout.Height(GetContentPaneHeight(24f)));
                    break;
                case MenuPage.BikeMaterials:
                    BikeMaterialsLoader.DrawBikeMaterialsTabUI();
                    break;
                case MenuPage.RiderAppearance:
                    Custom.DrawCharacterTab();
                    break;
                case MenuPage.RiderEmotes:
                    DrawRiderEmotesPage();
                    break;
                case MenuPage.RiderTools:
                    RiderStyleEditor.DrawTab();
                    break;
                case MenuPage.BikeStudio:
                    BikePoseEditor.DrawTab();
                    break;
                case MenuPage.CameraGameplay:
                    DrawCameraGameplayPage();
                    break;
                case MenuPage.CameraLens:
                    DrawReplayLensPage();
                    break;
                case MenuPage.CameraFocus:
                    DrawReplayFocusPage();
                    break;
                case MenuPage.CameraFraming:
                    DrawReplayFramingPage();
                    break;
                case MenuPage.CameraLight:
                    DrawReplayLightPage();
                    break;
                case MenuPage.CameraPresets:
                    DrawReplayPresetsPage();
                    break;
                case MenuPage.WorldDropper:
                    ObjectDropper.DrawDropperTab();
                    break;
                case MenuPage.WorldMarker:
                    DrawWorldMarkerPage();
                    break;
                case MenuPage.WorldDrone:
                    DrawWorldDronePage();
                    break;
                case MenuPage.WorldVehicleSpawns:
                    DrawWorldVehicleSpawnsPage();
                    break;
                case MenuPage.GraphicsPerformance:
                    GraphicsEnvironmentController.DrawPerformancePage();
                    break;
                case MenuPage.GraphicsEnvironment:
                    GraphicsEnvironmentController.DrawEnvironmentPage();
                    break;
                case MenuPage.GraphicsSceneLights:
                    DrawSceneLightsPage();
                    break;
                case MenuPage.AdvancedStartup:
                    DrawStartupPage();
                    break;
                case MenuPage.AdvancedInterface:
                    DrawInterfacePage();
                    break;
                case MenuPage.AdvancedMultiplayer:
                    DrawMultiplayerPage();
                    break;
                case MenuPage.AdvancedDeveloper:
                    DebugTools.DrawDebugTab();
                    break;
            }
        }

        private static void DrawSettingsSearchResults()
        {
            string query = (_menuSearch ?? string.Empty).Trim();
            string[] terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            BeginPane("Results", "Choose a result to open its exact area and page.");
            int matches = 0;
            for (int i = 0; i < NavigationPages.Length; i++)
            {
                PageDefinition page = NavigationPages[i];
                string haystack = $"{GetAreaLabel(page.Area)} {page.Label} {page.Description} {page.SearchTerms}";
                bool matched = true;
                for (int termIndex = 0; termIndex < terms.Length; termIndex++)
                {
                    if (haystack.IndexOf(terms[termIndex], StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    matched = false;
                    break;
                }

                if (!matched)
                    continue;

                matches++;
                if (ControllerButton($"search_result_{page.Page}",
                        $"{GetAreaLabel(page.Area)}  ›  {page.Label}", UiRowButtonStyle,
                        GUILayout.Height(36f * UiScale)))
                {
                    _menuSearch = string.Empty;
                    SelectPage(page.Page);
                }
                GUILayout.Label(page.Description, UiMutedWrappedStyle);
                GUILayout.Space(6f);
            }

            if (matches == 0)
                DrawEmptyState("No matching settings", "Try a simpler phrase such as fisheye, night sky, spin, rider, or marker.");
            EndPane();
        }

        private static void DrawRideHandlingPage()
        {
            bool previousGuiChanged = GUI.changed;
            GUI.changed = false;

            PhysicsPreset.DrawControls();

            BeginPane("Core Handling", "The most common riding assists and bike-response controls.");
            ModernToggle("Spin Assist", ref physics.spinAssist);
            ModernToggle("Drifting", ref physics.driftAbility);
            bool disableTweaking = Config.trickAnimationDebugSettings.disableTweaking;
            ModernToggle("Disable Trick Tweaking", ref disableTweaking, "ride_disable_trick_tweaking");
            if (disableTweaking != Config.trickAnimationDebugSettings.disableTweaking)
            {
                Config.trickAnimationDebugSettings.disableTweaking = disableTweaking;
                TrickTweakGuard.ApplySettingChange();
                Config.RequestSave();
            }
            if (disableTweaking)
                GUILayout.Label(TrickTweakGuard.HookStatus, UiMutedWrappedStyle);
            Slider("Gravity", ref physics.gravity, 12.5f, 0f, 30f);
            Slider("Small Hop Force", ref physics.smallHopForce, 4.2f, 0f, 25f);
            ModernToggle("Custom Physics Update Rate", ref physics.useCustomPhysicsStepRate, "physics_custom_step_rate");
            float gamePhysicsRate = Mods.Physics.GamePhysicsRate;
            if (physics.useCustomPhysicsStepRate)
            {
                if (!float.IsFinite(physics.physicsStepRate) || physics.physicsStepRate <= 0f)
                    physics.physicsStepRate = gamePhysicsRate;

                ModernToggle(
                    "Allow Rates Above Game Default (Larger Replay Buffer)",
                    ref physics.allowHighPhysicsRateWithExtendedReplay,
                    "physics_allow_extended_replay");
                float maximumPhysicsRate = physics.allowHighPhysicsRateWithExtendedReplay
                    ? 250f
                    : Mathf.Max(30f, gamePhysicsRate);
                Slider(
                    "Physics Update Rate (Hz)",
                    ref physics.physicsStepRate,
                    gamePhysicsRate,
                    30f,
                    maximumPhysicsRate);

                if (physics.allowHighPhysicsRateWithExtendedReplay &&
                    physics.physicsStepRate > gamePhysicsRate)
                {
                    float replayMemoryMultiplier = physics.physicsStepRate / gamePhysicsRate;
                    GUILayout.Label(
                        $"Replay protection: RoweMod requests about {replayMemoryMultiplier:0.##}x the normal " +
                        "native replay samples to preserve duration. This uses more memory.",
                        UiMutedWrappedStyle);
                }
            }
            GUILayout.Label(
                $"Game default detected: {gamePhysicsRate:0.##} Hz. Lower uses less CPU but can reduce " +
                "landing/contact stability. Higher rates require a proportionally larger replay buffer.",
                UiMutedWrappedStyle);
            EndPane();

            BeginAltPane(
                "Coping Finder",
                "Tunes the native tire-ride system armed by holding RB with throttle as the jump begins.");
            GUILayout.Label(CopingFinderControl.StatusText, UiMutedWrappedStyle);
            if (!physics.copingFinderTuningEnabled)
            {
                GUILayout.Label(
                    "Game values are untouched. Start from the live bike's current values before changing anything.",
                    UiMutedWrappedStyle);
                if (PrimaryButton("Customize Current Game Values", GUILayout.Width(250f), GUILayout.Height(28f)))
                {
                    if (CopingFinderControl.BeginCustomization())
                        GUI.changed = true;
                }
            }
            else
            {
                ModernToggle("Magnet Pull Enabled", ref physics.copingFinderMagnetEnabled,
                    "coping_finder_magnet_enabled");
                ModernToggle("Allow Steering Alignment", ref physics.copingFinderUserAlignment,
                    "coping_finder_user_alignment");
                ModernToggle("Auto Rotation Assist", ref physics.grindAlignAssist,
                    "coping_finder_alignment_assist");
                Slider("Search Range Multiplier", ref physics.copingFinderSearchRange, 1f, 0.25f, 3f);
                Slider("Pull Strength Multiplier", ref physics.grindAssistStrength, 0.5f, 0.01f, 5f);
                Slider("Detector Rotation Offset", ref physics.copingFinderRotation, 0f, -45f, 45f);
                GUILayout.Label(
                    "Search range changes how far the native corner detector looks. Pull strength changes the lateral magnet force. Rotation offsets the detector pivot in degrees.",
                    UiMutedWrappedStyle);
                BeginToolbar();
                if (SecondaryButton("Use Current Live Values", GUILayout.Width(185f), GUILayout.Height(27f)))
                {
                    if (CopingFinderControl.CopyLiveValuesToConfig())
                        GUI.changed = true;
                }
                if (SecondaryButton("Restore Game Values", GUILayout.Width(170f), GUILayout.Height(27f)))
                {
                    CopingFinderControl.RestoreGameValues();
                    GUI.changed = true;
                }
                EndToolbar();
            }
            EndPane();

            BeginAltPane(
                "Transition Scanner",
                "Tunes the native surface scanner used to find transition landings. Detection ownership stays with the game.");
            GUILayout.Label(TransitionSettingsControl.StatusText, UiMutedWrappedStyle);
            if (!physics.transitionTuningEnabled)
            {
                GUILayout.Label(
                    "Game values are untouched. Customization starts from the current bike's live TransitionSettings asset.",
                    UiMutedWrappedStyle);
                if (PrimaryButton("Customize Current Game Values", GUILayout.Width(250f), GUILayout.Height(28f)))
                {
                    if (TransitionSettingsControl.BeginCustomization())
                        GUI.changed = true;
                }
            }
            else
            {
                GUILayout.Label("Detection Range", UiMutedWrappedStyle);
                Slider("Accepted Surface Slope", ref physics.transitionAcceptableSlope,
                    TransitionSettingsControl.NativeAcceptableSlope, 0f, 90f);
                Slider("Base Scan Radius", ref physics.transitionScanRadius,
                    TransitionSettingsControl.NativeScanRadius, 0.01f, 10f);
                Slider("Velocity Radius Minimum", ref physics.transitionScanRadiusPerVelocityMin,
                    TransitionSettingsControl.NativeScanRadiusPerVelocityMin, 0f, 10f);
                Slider("Velocity Radius Maximum", ref physics.transitionScanRadiusPerVelocityMax,
                    TransitionSettingsControl.NativeScanRadiusPerVelocityMax, 0f, 10f);

                GUILayout.Label("Scan Shape", UiMutedWrappedStyle);
                IntegerSlider("Horizontal Scan Rays", ref physics.transitionHorizontalRays,
                    TransitionSettingsControl.NativeHorizontalRays, 1, 32);
                IntegerSlider("Vertical Scan Rays", ref physics.transitionVerticalRays,
                    TransitionSettingsControl.NativeVerticalRays, 1, 32);
                Slider("Minimum Surface Up Dot", ref physics.transitionMinimumYDot,
                    TransitionSettingsControl.NativeMinimumYDot, 0f, 1f);
                ModernToggle("Scan Under the Bike", ref physics.transitionRunUnderScanner,
                    "transition_run_under_scanner");
                Slider("Side Fallback Scan Length", ref physics.transitionSecondarySideScannerLength,
                    TransitionSettingsControl.NativeSecondarySideScannerLength, 0f, 20f);
                Slider("Under-Bike Fallback Length", ref physics.transitionSecondaryUnderScannerLength,
                    TransitionSettingsControl.NativeSecondaryUnderScannerLength, 0f, 20f);
                GUILayout.Label(
                    "More rays sample the landing surface more densely but cost more physics work. The up-dot threshold rejects surfaces whose normal is too far from upright.",
                    UiMutedWrappedStyle);

                BeginToolbar();
                if (SecondaryButton("Use Current Live Values", GUILayout.Width(185f), GUILayout.Height(27f)))
                {
                    if (TransitionSettingsControl.CopyLiveValuesToConfig())
                        GUI.changed = true;
                }
                if (SecondaryButton("Restore Game Values", GUILayout.Width(170f), GUILayout.Height(27f)))
                {
                    TransitionSettingsControl.RestoreGameValues();
                    GUI.changed = true;
                }
                EndToolbar();
            }
            EndPane();

            BeginAltPane("Pump, Spin & Manuals", "Less commonly changed riding-response controls.");
            Slider("Pump Force", ref physics.pumpForce, 1.5f, 1f, 5f);
            Slider("Spin Speed Multiplier", ref physics.spinMultiplier, 1f, 0f, 10f);
            Slider("Steer Damping", ref physics.steerDamp, 5f, 0f, 5f);
            Slider("Max Nose Manual Angle", ref physics.noseManualAngle, 30f, 10f, 50f);
            Slider("Max Manual Angle", ref physics.manualAngle, 30f, 10f, 50f);
            GUILayout.Label("Nose Manual Variants (Hang 5)", UiMutedWrappedStyle);
            int previousHangFiveMode = physics.hangFiveMode;
            BeginToolbar();
            if (PillButton("Game", physics.hangFiveMode == 0, GUILayout.ExpandWidth(true)))
                physics.hangFiveMode = 0;
            if (PillButton("Disabled", physics.hangFiveMode == 1, GUILayout.ExpandWidth(true)))
                physics.hangFiveMode = 1;
            if (PillButton("Swap Sides", physics.hangFiveMode == 2, GUILayout.ExpandWidth(true)))
                physics.hangFiveMode = 2;
            EndToolbar();
            if (physics.hangFiveMode != previousHangFiveMode)
            {
                HangFiveControl.ApplySettingChange();
                GUI.changed = true;
            }
            GUILayout.Label(
                "Disabled blocks the Hang 5 animation variants and keeps a normal nose manual. Swap Sides reverses the bumper-selected variants. Other bumper actions are unchanged.",
                UiMutedWrappedStyle);
            GUILayout.Label(HangFiveControl.HookStatus, UiMutedWrappedStyle);
            ModernToggle("Nose Manual COM / Inertia Tuning", ref physics.noseManualTurnTuning);
            if (physics.noseManualTurnTuning)
            {
                DrawSectionTitle("Advanced Nose Manual Balance",
                    "Offsets are local to the bike and restore after the physical nose pivot ends.");
                Slider("Chassis COM Forward", ref physics.noseManualChassisComForwardOffset, 0f, -1f, 1f);
                Slider("Chassis COM Height", ref physics.noseManualChassisComVerticalOffset, 0f, -1f, 1f);
                Slider("Rider COM Forward", ref physics.noseManualDriverComForwardOffset, 0f, -1f, 1f);
                Slider("Rider COM Height", ref physics.noseManualDriverComVerticalOffset, 0f, -1f, 1f);
                Slider("Rider COM Turn Lean", ref physics.noseManualComTurnLean, 0f, -0.5f, 0.5f);
                Slider("Nose Rider Inertia", ref physics.noseManualDriverInertiaMultiplier, 1f, 0.25f, 3f);
                ModernToggle("Debug Nose Manual Logs", ref physics.noseManualDebugLogging);
            }
            EndPane();

            bool changed = GUI.changed;
            GUI.changed |= previousGuiChanged;
            if (changed)
                Mods.Physics.Update();
        }

        public static void OpenVehicleTuningPage()
        {
            _menuSearch = string.Empty;
            SelectPage(MenuPage.RideVehicleTuning);
            Main.OpenRoweModMenu();
        }

        private static void IntegerSlider(string label, ref int target, int defaultValue, int min, int max)
        {
            float value = target;
            Slider(label, ref value, defaultValue, min, max);
            target = Mathf.Clamp(Mathf.RoundToInt(value), min, max);
        }

        private static void PredictionIntegerSlider(string label, ref int target, int min, int max, string controlId)
        {
            float value = target;
            ModernSlider(label, ref value, min, max, controlId);
            target = Mathf.Clamp(Mathf.RoundToInt(value), min, max);
        }

        private static void DrawPredictionLabPage()
        {
            bool previousGuiChanged = GUI.changed;
            GUI.changed = false;
            PredictionLabSettings settings = predictionLabSettings;

            BeginPane("Runtime Topology & One-Jump Recorder",
                "Read-only diagnostics for shared prediction assets, activation order, forces, targets, balance, and rider-joint error.");
            GUILayout.Label(PredictionRuntimeDiagnostics.Status, UiMutedWrappedStyle);
            GUILayout.Label(PredictionRuntimeDiagnostics.TopologySummary, UiMutedWrappedStyle);
            BeginToolbar();
            if (SecondaryButton("Refresh Topology", GUILayout.Width(170f), GUILayout.Height(28f)))
                PredictionRuntimeDiagnostics.RefreshTopology();
            if (!PredictionRuntimeDiagnostics.CanCancel)
            {
                if (PrimaryButton("Arm One-Jump Capture", GUILayout.Width(190f), GUILayout.Height(28f)))
                    PredictionRuntimeDiagnostics.ArmOneJumpCapture();
            }
            else if (SecondaryButton("Cancel Capture", GUILayout.Width(150f), GUILayout.Height(28f)))
            {
                PredictionRuntimeDiagnostics.CancelCapture();
            }
            EndToolbar();
            if (!string.IsNullOrWhiteSpace(PredictionRuntimeDiagnostics.LastReportPath))
                GUILayout.Label("Topology report: " + PredictionRuntimeDiagnostics.LastReportPath, UiMutedWrappedStyle);
            if (!string.IsNullOrWhiteSpace(PredictionRuntimeDiagnostics.LastCapturePath))
                GUILayout.Label("Last jump capture: " + PredictionRuntimeDiagnostics.LastCapturePath, UiMutedWrappedStyle);
            GUILayout.Label(
                "Arm while on the ground, close the menu, perform one jump, and land. The recorder samples existing state only and saves a CSV under RoweMod\\Diagnostics.",
                UiMutedWrappedStyle);
            EndPane();

            BeginPane("Predicted Landing Display",
                "Draws a depth-tested arc from the game's native landing result. It adds no rigidbody, collider, joint, or force.");
            GUILayout.Label(PredictionSystemsControl.PredictionStatus, UiMutedWrappedStyle);
            ModernToggle("Show Predicted Landing", ref settings.visualizationEnabled, "prediction_visual_enabled");
            if (settings.visualizationEnabled)
            {
                ModernToggle("Flight Arc", ref settings.visualizationShowArc, "prediction_visual_arc");
                ModernToggle("Landing Cross", ref settings.visualizationShowLandingMarker, "prediction_visual_marker");
                ModernToggle("Landing Surface Normal", ref settings.visualizationShowSurfaceNormal, "prediction_visual_normal");
                PredictionIntegerSlider("Arc Segments", ref settings.visualizationArcSegments, 6, 96,
                    "prediction_visual_segments");
                ModernSlider("Line Width", ref settings.visualizationLineWidth, 0.005f, 0.15f,
                    "prediction_visual_width");
                ModernSlider("Marker Size", ref settings.visualizationMarkerSize, 0.05f, 2f,
                    "prediction_visual_marker_size");
                GUILayout.Label("Display Color", UiMutedWrappedStyle);
                ModernSlider("Red", ref settings.visualizationColorR, 0f, 1f, "prediction_visual_r");
                ModernSlider("Green", ref settings.visualizationColorG, 0f, 1f, "prediction_visual_g");
                ModernSlider("Blue", ref settings.visualizationColorB, 0f, 1f, "prediction_visual_b");
            }
            GUILayout.Label(
                "The retail game's original DrawFlightPath routine is stripped, so RoweMod reconstructs the display from its launch point, apex, landing point, and landing normal.",
                UiMutedWrappedStyle);
            EndPane();

            BeginAltPane("Flight Landing Assist",
                "Tunes the native force correction that steers the bike toward a predicted landing.");
            GUILayout.Label(PredictionSystemsControl.FlightStatus, UiMutedWrappedStyle);
            if (!settings.flightAugmentTuningEnabled)
            {
                DrawPredictionOptIn(
                    "Game values are untouched. Capture the current bike's FlightAugmentSettings before editing.",
                    PredictionSystemsControl.BeginFlightCustomization);
            }
            else
            {
                ModernSlider("Correction Strength", ref settings.flightCorrectionStrength, 0f, 100f,
                    "prediction_flight_strength");
                ModernSlider("Force Cap", ref settings.flightForceCap, 0f, 1000f,
                    "prediction_flight_force_cap");
                ModernSlider("Surface Normal Push", ref settings.flightNormalPushOff, -100f, 100f,
                    "prediction_flight_push");
                ModernSlider("Maximum Velocity Adjustment", ref settings.flightMaximumVelocityAdjust, 0f, 100f,
                    "prediction_flight_velocity_adjust");
                ModernSlider("Minimum Air Time", ref settings.flightMinimumAirTime, 0f, 5f,
                    "prediction_flight_min_air");
                ModernSlider("Maximum Correction Time", ref settings.flightMaximumTickTime, 0f, 5f,
                    "prediction_flight_max_tick");
                ModernSlider("Landing Pitch", ref settings.flightLandingPitch, -90f, 90f,
                    "prediction_flight_pitch");
                ModernToggle("Choose Closest Point to Body", ref settings.flightClosestToBody,
                    "prediction_flight_closest");
                ModernToggle("Choose Furthest Point", ref settings.flightFurthestPoint,
                    "prediction_flight_furthest");
                ModernToggle("Choose Steepest Angle", ref settings.flightSteepestAngle,
                    "prediction_flight_steepest");
                ModernToggle("Stop Correcting While Falling", ref settings.flightDoNotTickIfFalling,
                    "prediction_flight_no_fall");
                DrawPredictionRestoreButton("Restore Flight Augment Game Values", PredictionSystemsControl.RestoreFlight);
            }
            EndPane();

            BeginPane("Flight Prediction",
                "Controls how the game searches the projected path and samples a landing surface.");
            GUILayout.Label(PredictionSystemsControl.PredictionStatus, UiMutedWrappedStyle);
            if (!settings.predictionTuningEnabled)
            {
                DrawPredictionOptIn(
                    "Game values are untouched. Capture the live FlightPredictionSettings asset before editing.",
                    PredictionSystemsControl.BeginPredictionCustomization);
            }
            else
            {
                PredictionIntegerSlider("Simulation Steps", ref settings.predictionSteps, 1, 1000,
                    "prediction_steps");
                ModernSlider("Time per Step", ref settings.predictionPathTimeStep, 0.001f, 0.2f,
                    "prediction_time_step");
                ModernSlider("Start Height Offset", ref settings.predictionStartYOffset, -1f, 1f,
                    "prediction_start_y");
                ModernSlider("Seconds Between Predictions", ref settings.predictionTickInterval, 0f, 0.2f,
                    "prediction_tick_interval");
                ModernSlider("Cast Radius", ref settings.predictionCastRadius, 0f, 1f,
                    "prediction_cast_radius");
                ModernSlider("Minimum Cast Radius", ref settings.predictionCastRadiusMin, 0f, 1f,
                    "prediction_cast_radius_min");
                GUILayout.Label("Landing Surface Sampling", UiMutedWrappedStyle);
                PredictionIntegerSlider("Normal Sample Rays", ref settings.predictionSurfaceNormalRays, 1, 128,
                    "prediction_normal_rays");
                ModernSlider("Normal Cast Distance", ref settings.predictionSurfaceNormalMaxDistance, 0f, 20f,
                    "prediction_normal_distance");
                ModernSlider("Normal Start Offset", ref settings.predictionSurfaceNormalStartOffset, 0f, 10f,
                    "prediction_normal_offset");
                ModernSlider("Normal Ray Spread", ref settings.predictionSurfaceNormalSpread, 0f, 10f,
                    "prediction_normal_spread");
                ModernToggle("Prefer Rider Up Direction", ref settings.predictionPreferPlayerUp,
                    "prediction_prefer_up");
                ModernToggle("Prefer Transition Angle", ref settings.predictionPreferTransitionAngle,
                    "prediction_prefer_transition");
                if (settings.predictionPreferTransitionAngle)
                {
                    ModernSlider("Wanted Normal X", ref settings.predictionWantedNormalX, -1f, 1f,
                        "prediction_normal_x");
                    ModernSlider("Wanted Normal Y", ref settings.predictionWantedNormalY, -1f, 1f,
                        "prediction_normal_y");
                    ModernSlider("Wanted Normal Z", ref settings.predictionWantedNormalZ, -1f, 1f,
                        "prediction_normal_z");
                }
                ModernSlider("Landing Normal Pitch", ref settings.predictionLandingNormalPitch, -90f, 90f,
                    "prediction_landing_normal_pitch");
                GUILayout.Label(
                    "More steps and surface rays improve sampling density but increase physics-query cost.",
                    UiMutedWrappedStyle);
                DrawPredictionRestoreButton("Restore Flight Prediction Game Values", PredictionSystemsControl.RestorePrediction);
            }
            EndPane();

            BeginAltPane("Coping Corner Detector",
                "Exposes the native ray fan used to identify coping and usable lip edges.");
            GUILayout.Label(PredictionSystemsControl.CornerStatus, UiMutedWrappedStyle);
            if (!settings.cornerDetectorTuningEnabled)
            {
                DrawPredictionOptIn(
                    "Game values are untouched. Capture the current CornerDetector before editing.",
                    PredictionSystemsControl.BeginCornerCustomization);
            }
            else
            {
                ModernSlider("Distance Multiplier", ref settings.cornerDistanceMultiplier, 0.1f, 10f,
                    "prediction_corner_distance_mult");
                ModernSlider("Corner Check Distance", ref settings.cornerCheckDistance, 0.1f, 10f,
                    "prediction_corner_distance");
                PredictionIntegerSlider("Ray Count", ref settings.cornerNumberOfRays, 4, 360,
                    "prediction_corner_rays");
                ModernSlider("Facing Dot Threshold", ref settings.cornerFacingDot, 0f, 1f,
                    "prediction_corner_facing");
                ModernSlider("Up Normal Threshold", ref settings.cornerYNormalThreshold, 0f, 1f,
                    "prediction_corner_y_normal");
                ModernSlider("Ray Start Height", ref settings.cornerStartUpOffset, 0f, 1f,
                    "prediction_corner_start_y");
                ModernSlider("Maximum Edge Walk", ref settings.cornerEdgeCheckMaximum, 0.25f, 20f,
                    "prediction_corner_edge_max");
                ModernSlider("Edge Walk Step", ref settings.cornerEdgeStep, 0.01f, 0.5f,
                    "prediction_corner_edge_step");
                ModernSlider("Edge Probe Outset", ref settings.cornerEdgeProbeOutset, 0f, 0.2f,
                    "prediction_corner_edge_outset");
                ModernSlider("Edge Angle Tolerance", ref settings.cornerEdgeAngleTolerance, 0f, 30f,
                    "prediction_corner_angle");
                PredictionIntegerSlider("Allowed Missed Probes", ref settings.cornerMissesAllowed, 0, 2,
                    "prediction_corner_misses");
                ModernSlider("Down Probe Distance", ref settings.cornerDownTestDistance, 0f, 0.5f,
                    "prediction_corner_down");
                ModernSlider("Up-Walk Probe Distance", ref settings.cornerUpWalkTestDistance, 0f, 0.5f,
                    "prediction_corner_up");
                ModernSlider("Inward Probe Distance", ref settings.cornerInwardTestDistance, 0f, 0.5f,
                    "prediction_corner_inward");
                DrawPredictionRestoreButton("Restore Corner Detector Game Values", PredictionSystemsControl.RestoreCorner);
            }
            EndPane();

            BeginPane("Lip Magnet",
                "Tunes the native edge magnet and its optional flight-prediction handoff.");
            GUILayout.Label(PredictionSystemsControl.LipStatus, UiMutedWrappedStyle);
            if (!settings.lipMagnetTuningEnabled)
            {
                DrawPredictionOptIn(
                    "Game values are untouched. Capture the current LipMagnet before editing.",
                    PredictionSystemsControl.BeginLipCustomization);
            }
            else
            {
                ModernSlider("Pull Power Multiplier", ref settings.lipPowerMultiplier, 0f, 1f,
                    "prediction_lip_power");
                ModernSlider("Pull Length Multiplier", ref settings.lipLengthMultiplier, 0f, 3f,
                    "prediction_lip_length");
                ModernSlider("Surface Normal Push", ref settings.lipNormalPushOff, -0.3f, 0.3f,
                    "prediction_lip_push");
                ModernSlider("Minimum Height from Flight Apex", ref settings.lipMinimumYFromFlightApex, -1f, 1f,
                    "prediction_lip_apex_y");
                ModernSlider("Minimum Target Distance", ref settings.lipMinimumDistance, -0.5f, 0.5f,
                    "prediction_lip_min_distance");
                ModernSlider("Distance Boost", ref settings.lipDistanceBoost, -0.5f, 0.5f,
                    "prediction_lip_distance_boost");
                ModernSlider("Maximum Magnet Velocity", ref settings.lipMaximumMagnetVelocity, 0f, 100f,
                    "prediction_lip_max_velocity");
                ModernSlider("Rotation Offset", ref settings.lipRotation, -90f, 90f,
                    "prediction_lip_rotation");
                ModernSlider("Move Delta Multiplier", ref settings.lipMoveDeltaMultiplier, 0f, 10f,
                    "prediction_lip_delta_mult");
                ModernSlider("Maximum Move Delta", ref settings.lipMaximumMoveDelta, 0f, 10f,
                    "prediction_lip_delta_max");
                ModernToggle("Alignment Assist", ref settings.lipAlignmentAssist, "prediction_lip_align");
                ModernToggle("Override Vertical Motion", ref settings.lipOverrideVertical,
                    "prediction_lip_override_vertical");
                ModernToggle("Run Flight Prediction", ref settings.lipRunFlightPrediction,
                    "prediction_lip_run_prediction");
                ModernToggle("Prefer Vertical Assist", ref settings.lipPreferVerticalAssist,
                    "prediction_lip_prefer_vertical");
                ModernToggle("Allow Negative Push", ref settings.lipAllowNegativePush,
                    "prediction_lip_negative_push");
                DrawPredictionRestoreButton("Restore Lip Magnet Game Values", PredictionSystemsControl.RestoreLip);
            }
            EndPane();

            BeginAltPane("Rider Physics Body", "Read-only inventory of the local rider's physical animation system.");
            GUILayout.Label(PredictionSystemsControl.RagdollStatus, UiMutedWrappedStyle);
            GUILayout.Label(
                "The rider is an active ragdoll: physics bones have Rigidbody and ConfigurableJoint components driven by spring, damper, maximum-force, positional-strength, and PID values. Those drives act like muscles, but the game does not use PuppetMaster Muscle objects.",
                UiMutedWrappedStyle);
            GUILayout.Label(
                "Prediction visualization does not duplicate or modify this body. Joint-drive tuning is intentionally read-only until each bone's ownership and recovery behavior can be tested safely.",
                UiMutedWrappedStyle);
            EndPane();

            bool changed = GUI.changed;
            GUI.changed |= previousGuiChanged;
            if (changed)
            {
                PredictionSystemsControl.ApplyAll();
                Config.RequestSave();
            }
        }

        private static void DrawPredictionOptIn(string explanation, Func<bool> beginCustomization)
        {
            GUILayout.Label(explanation, UiMutedWrappedStyle);
            if (PrimaryButton("Customize Current Game Values", GUILayout.Width(250f), GUILayout.Height(28f)) &&
                beginCustomization())
            {
                GUI.changed = true;
            }
        }

        private static void DrawPredictionRestoreButton(string label, Action restore)
        {
            BeginToolbar();
            if (SecondaryButton(label, GUILayout.Width(250f), GUILayout.Height(27f)))
            {
                restore();
                GUI.changed = true;
            }
            EndToolbar();
        }

        private static void DrawRideSafetyPage()
        {
            BeginPane("Bails & Injuries", "These settings change recovery and injury behavior, not bike handling.");
            ModernToggle("No Bail", ref misc.neverBail);
            bool boneBreakingEnabled = !misc.disableBoneBreaking;
            ModernToggle("Bone Breaking", ref boneBreakingEnabled, "ride_bone_breaking");
            bool disableBoneBreaking = !boneBreakingEnabled;
            if (disableBoneBreaking != misc.disableBoneBreaking)
            {
                misc.disableBoneBreaking = disableBoneBreaking;
                Mods.Misc.ApplyBoneBreakingState(true);
            }

            if (boneBreakingEnabled)
            {
                float previousStrength = misc.boneBreakingStrength;
                Slider("Bone Strength", ref misc.boneBreakingStrength, 1f, 0.25f, 5f);
                if (!Mathf.Approximately(previousStrength, misc.boneBreakingStrength))
                    Mods.Misc.ApplyBoneBreakingState(true);
            }
            EndPane();
        }

        private static void DrawRideVehicleTuningPage()
        {
            bool previousGuiChanged = GUI.changed;
            GUI.changed = false;

            BeginPane("Everyday Motor Controls", "Vehicles without a custom entry use these global values.");
            Slider("Global Push Force", ref physics.bmxForceFactor, 0.07f, 0.05f, 2f);
            Slider("Global Max Speed", ref physics.bmxMaxSpeed, 7.5f, 2f, 15f);
            EndPane();

            BeginPane("Per-Vehicle Motor Tuning", "Opt in individual vehicles without changing every physics field.");
            if (_motorTuningNeedsRefresh)
                RefreshMotorTuningData();
            DrawMotorTuningData();
            EndPane();

            BeginAltPane("Native Vehicle Settings",
                "Search and edit the complete live vehicle settings. Vehicle presets remain saved as reusable JSON files.");
            RuntimeVehicleTuneResetSupport.DrawEmbeddedInspector();
            GUILayout.Label("Shortcut: Ctrl + Shift + U opens this RoweMod page.", UiMutedWrappedStyle);
            EndPane();

            bool changed = GUI.changed;
            GUI.changed |= previousGuiChanged;
            if (changed)
                Mods.Physics.Update();
        }

        private static void DrawCameraGameplayPage()
        {
            BeginPane("Gameplay Camera", "Shortcuts that run during normal gameplay while the RoweMod menu is closed.");
            bool leftStickOffsetSwitch = Config.cameraSettings.leftStickOffsetSwitch;
            ModernToggle("Left Stick Tap Flips Camera Offset", ref leftStickOffsetSwitch,
                "camera_left_stick_offset_switch_grouped");
            if (leftStickOffsetSwitch != Config.cameraSettings.leftStickOffsetSwitch)
                Config.cameraSettings.leftStickOffsetSwitch = leftStickOffsetSwitch;
            GUILayout.Label(
                "Release LS before 0.5 seconds to flip the camera. Holding it for 0.5 seconds is reserved for Bike-Only Stance.",
                UiMutedWrappedStyle);
            EndPane();

            BeginAltPane("Replay Collision", "Keep the free camera from being blocked by map collision when desired.");
            ModernToggle("Disable Replay Camera Collider", ref misc.disableFreeCamCollider,
                "camera_disable_replay_collider");
            EndPane();
        }

        private static void DrawReplayLensPage()
        {
            BeginPane("Replay Camera", "Native FOV and tilt remain authoritative on the replay timeline.");
            ReplayCameraLight.DrawCameraControls("camera_area_camera_");
            EndPane();
            BeginPane("Lens Character", "Fisheye optics, crop, projection character, vignette, and shake.");
            ReplayCameraLight.DrawLensControls("camera_area_lens_");
            EndPane();
            DrawReplayKeyframeStrip();
        }

        private static void DrawReplayFocusPage()
        {
            BeginPane("Depth of Field", "Keyframe the complete native near/far focus model.");
            ReplayCameraLight.DrawDofControls("camera_area_focus_");
            EndPane();
            DrawReplayKeyframeStrip();
        }

        private static void DrawReplayFramingPage()
        {
            BeginPane("Framing", "Capture-safe aspect mattes never change the game's output resolution.");
            ReplayCameraLight.DrawFramingControls("camera_area_frame_");
            EndPane();
            DrawReplayKeyframeStrip();
        }

        private static void DrawReplayLightPage()
        {
            BeginPane("Replay Camera Light", "A high-quality replay-only local light attached to the active camera.");
            ReplayCameraLight.DrawLightControls("camera_area_light_");
            EndPane();
            DrawReplayKeyframeStrip();
        }

        private static void DrawReplayPresetsPage()
        {
            BeginPane("Replay Camera Presets",
                "Save lens, focus, shake, framing, and the replay light as one reusable look.");
            ReplayCameraLight.DrawPresetControls();
            EndPane();
            DrawReplayKeyframeStrip();
        }

        private static void DrawReplayKeyframeStrip()
        {
            BeginAltPane("Replay Keyframes", "Native Add/Delete commands update RoweMod camera tracks too.");
            ReplayCameraLight.DrawKeyframeControls();
            EndPane();
        }

        private static void DrawWorldMarkerPage()
        {
            BeginPane("Session Marker", "Choose a marker prefab replacement for session markers.");
            int count = 0;
            foreach (GameObject marker in sessionMarkers)
            {
                if (marker == null)
                    continue;

                count++;
                if (ControllerButton($"marker_{marker.name}", marker.name, UiRowButtonStyle,
                        GUILayout.Height(36f * UiScale)))
                {
                    ReplaceSessionMarkerWithPrefab(marker);
                    Config.misc.customSessionMarker = marker.name;
                }
            }

            if (count == 0)
                DrawEmptyState("No session markers found", "Load into gameplay or refresh marker data, then reopen this page.");
            GUILayout.Space(8f);
            GUILayout.Label("Selected marker: " + (Config.misc.customSessionMarker ?? "None"), UiMutedWrappedStyle);
            EndPane();
        }

        private static void DrawWorldDronePage()
        {
            BeginPane("Drone", "Control the parts of the game drone that are active and visible.");
            ModernToggle("Drone Body", ref misc.droneBodyToggle, "world_drone_body");
            ModernToggle("Drone Sound", ref misc.droneEmitterToggle, "world_drone_sound");
            ModernToggle("Disable Drone Colliders", ref misc.disableDroneCollider, "world_drone_colliders");
            Slider("Drone Mass", ref misc.droneMass, 10f, 2f, 1000f);
            EndPane();
        }

        private static void DrawWorldVehicleSpawnsPage()
        {
            BeginPane("Utility Vehicles", "Spawn a RoweMod vehicle in front of the current player.");
            if (PrimaryButton("Spawn Drift Car", GUILayout.Width(180f), GUILayout.Height(30f)))
                SpawnDriftCarInFrontOfPlayer();
            GUILayout.Space(6f);
            if (SecondaryButton("Spawn Drift Trike", GUILayout.Width(180f), GUILayout.Height(30f)))
                SpawnDriftTrikeInFrontOfPlayer();
            EndPane();

            BeginAltPane("Cleanup", "Remove temporary visual marks from the current map.");
            if (DangerButton("Remove Skidmarks", GUILayout.Width(180f), GUILayout.Height(30f)))
                Memory.RemoveSkidmarks();
            EndPane();
        }

        private static void DrawSceneLightsPage()
        {
            BeginToolbar();
            GUILayout.Label("Scene lights are scanned only when this page opens or when you request a refresh.",
                UiMutedWrappedStyle, GUILayout.ExpandWidth(true));
            if (SecondaryButton("Refresh Lights", GUILayout.Width(130f), GUILayout.Height(28f)))
                RefreshSceneLightCache();
            EndToolbar();
            GUILayout.Space(8f);
            DrawLightSettings();
        }

        private static void DrawStartupPage()
        {
            BeginPane("Startup", "Infrequently changed behavior used when entering the game.");
            ModernToggle("Skip Main Intro", ref Config.autoSkipIntro, "advanced_skip_intro");
            EndPane();
        }

        private static void DrawInterfacePage()
        {
            BeginPane("Interface Scale", "RoweMod combines this preference with a conservative screen-height scale.");
            ModernSlider("UI Scale", ref misc.menuScale, 0.8f, 1.35f, "interface_scale");
            GUILayout.Label($"Active effective scale: {EffectiveUiScale * 100f:0}%", UiMutedWrappedStyle);
            if (PrimaryButton("Apply Interface Scale", GUILayout.Width(185f), GUILayout.Height(30f)))
                ApplyConfiguredInterfaceScale();
            EndPane();

            BeginPane("Menu Accent", "Customize RoweMod's accent color without changing the game's interface.");
            ModernSlider("Red", ref misc.menuAccentR, 0f, 1f, "interface_accent_r");
            ModernSlider("Green", ref misc.menuAccentG, 0f, 1f, "interface_accent_g");
            ModernSlider("Blue", ref misc.menuAccentB, 0f, 1f, "interface_accent_b");
            if (PrimaryButton("Apply Menu Color", GUILayout.Width(170f), GUILayout.Height(30f)))
                stylesInitialized = false;
            EndPane();
        }

        public static void OpenEmotesPage()
        {
            _menuSearch = string.Empty;
            SelectPage(MenuPage.RiderEmotes);
            Main.OpenRoweModMenu();
        }

        private static void DrawRiderEmotesPage()
        {
            EmoteController.Refresh(false);
            EmotePropController.EnsureRadioVisual();
            VideoTvController.EnsureVisual();

            BeginPane("Native Emote System",
                "Stock emotes use the game's normal Play and Cancel path. Added poses use the same native animation layer locally.");
            GUILayout.Label(EmoteController.Status, UiMutedWrappedStyle);
            BeginToolbar();
            if (SecondaryButton("Refresh Emotes", GUILayout.Width(145f), GUILayout.Height(28f)))
                EmoteController.Refresh(true);
            if (SecondaryButton("Test Firecracker FX", GUILayout.Width(175f), GUILayout.Height(28f)))
                EmotePropController.TestFirecrackerFx();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = EmoteController.IsActive || EmoteController.IsLooping;
            if (SecondaryButton("Stop Emote", GUILayout.Width(125f), GUILayout.Height(28f)))
                EmoteController.Stop();
            GUI.enabled = previousEnabled;
            EndToolbar();
            GUILayout.Label(EmotePropController.TestStatus, UiMutedWrappedStyle);
            EndPane();

            BeginPane("Radio",
                "Play a song from the placed 3D radio. Song playback is independent from the rider's selected dance.");
            GUILayout.Label("YouTube link", UiRowMutedLabelStyle);
            RadioDanceController.YouTubeUrl = GUILayout.TextField(
                RadioDanceController.YouTubeUrl,
                UiSearchFieldStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(30f * UiScale));
            IReadOnlyList<RecentRadioSong> recentSongs = RadioDanceController.RecentSongs;
            if (recentSongs.Count > 0)
            {
                GUILayout.Label("Recently played", UiRowMutedLabelStyle);
                for (int recentIndex = 0; recentIndex < recentSongs.Count; recentIndex++)
                {
                    RecentRadioSong recentSong = recentSongs[recentIndex];
                    string recentTitle = recentSong?.title ?? "Untitled video";
                    if (recentTitle.Length > 72)
                        recentTitle = recentTitle.Substring(0, 69) + "...";
                    if (SecondaryButton(
                            recentTitle,
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(27f * UiScale)))
                        RadioDanceController.SelectRecentSong(recentIndex);
                }
            }
            float radioVolume = RadioDanceController.Volume;
            Slider("Radio Volume", ref radioVolume, 0.85f, 0f, 1f);
            RadioDanceController.Volume = radioVolume;
            bool multiplayerMediaEnabled = Config.replaySettings.multiplayerMediaEnabled;
            bool previousMultiplayerMediaEnabled = multiplayerMediaEnabled;
            ModernToggle(
                "Sync this station with RoweMod players",
                ref multiplayerMediaEnabled,
                "radio_multiplayer_media_sync");
            if (multiplayerMediaEnabled != previousMultiplayerMediaEnabled)
            {
                Config.replaySettings.multiplayerMediaEnabled = multiplayerMediaEnabled;
                Config.RequestSave();
                MultiplayerMediaSync.OnSettingChanged();
            }
            GUILayout.Label(MultiplayerMediaSync.Status, UiMutedWrappedStyle);
            GUILayout.Label(RadioDanceController.Status, UiMutedWrappedStyle);
            GUILayout.Label(EmotePropController.RadioVisualStatus, UiMutedWrappedStyle);
            BeginToolbar();
            previousEnabled = GUI.enabled;
            GUI.enabled = !RadioDanceController.IsBusy;
            if (PrimaryButton(
                    RadioDanceController.IsBusy ? "Preparing..." : "Play YouTube Radio",
                    GUILayout.Width(185f),
                    GUILayout.Height(30f)))
                RadioDanceController.StartFromYouTube();
            GUI.enabled = previousEnabled && (RadioDanceController.IsPlaying || RadioDanceController.IsBusy);
            if (SecondaryButton(RadioDanceController.IsBusy ? "Cancel" : "Stop Radio", GUILayout.Width(125f), GUILayout.Height(30f)))
                RadioDanceController.Stop();
            GUI.enabled = previousEnabled;
            EndToolbar();
            BeginToolbar();
            previousEnabled = GUI.enabled;
            GUI.enabled = !RadioDanceController.IsBusy;
            if (PrimaryButton(
                    RadioDanceController.IsBusy ? "Preparing..." : "Play Radio + TV",
                    GUILayout.Width(185f),
                    GUILayout.Height(30f)))
                RadioDanceController.StartRadioAndTvFromYouTube();
            GUI.enabled = previousEnabled && (VideoTvController.IsActive || VideoTvController.HasPendingVideo);
            if (SecondaryButton("Stop TV", GUILayout.Width(125f), GUILayout.Height(30f)))
                VideoTvController.Stop();
            GUI.enabled = previousEnabled;
            EndToolbar();
            GUILayout.Label(VideoTvController.Status, UiMutedWrappedStyle);
            GUILayout.Label(VideoTvController.VisualStatus, UiMutedWrappedStyle);
            GUILayout.Label(
                "First use downloads the official yt-dlp Windows helper and verifies it against the release SHA-256 list. " +
                "The radio remains where it was placed; FMOD pans it from the active camera, begins fading within the first meter, " +
                "and reaches silence at about 24 meters. Play Radio + TV also caches a compatible 720p MP4 and maps it only to the TV screen; " +
                "the TV is placed 2.6 meters in front of the live bike and uses the radio as its spatial audio source. " +
                "Multiplayer sync is opt-in and requires the same RoweMod build on each player; clients exchange only the station state, " +
                "then validate and cache the YouTube media themselves. Shared boombox physics is held kinematic so every client sees the same placement. " +
                "Only use audio you have permission to access.",
                UiMutedWrappedStyle);
            EndPane();

            BeginPane("Dance",
                "Choose a local dance independently. You can switch or stop the dance without interrupting the radio.");
            IReadOnlyList<EmoteController.EmoteOption> dances = EmoteController.DanceOptions;
            if (dances.Count == 0)
            {
                GUILayout.Label("No compatible local dance clips are loaded.", UiMutedWrappedStyle);
            }
            else
            {
                _selectedDanceIndex = Mathf.Clamp(_selectedDanceIndex, 0, dances.Count - 1);
                EmoteController.EmoteOption selectedDance = dances[_selectedDanceIndex];
                GUILayout.Label("Selected dance", UiRowMutedLabelStyle);
                GUILayout.Label(selectedDance.Label, UiRowLabelStyle);
                GUILayout.Label(selectedDance.ClipName, UiRowMutedLabelStyle);
                BeginToolbar();
                if (SecondaryButton("Previous", GUILayout.Width(110f), GUILayout.Height(30f)))
                    _selectedDanceIndex = (_selectedDanceIndex - 1 + dances.Count) % dances.Count;
                GUILayout.Label($"{_selectedDanceIndex + 1} of {dances.Count}", UiRowMutedLabelStyle, GUILayout.Width(70f));
                if (SecondaryButton("Next", GUILayout.Width(110f), GUILayout.Height(30f)))
                    _selectedDanceIndex = (_selectedDanceIndex + 1) % dances.Count;
                if (PrimaryButton("Loop Dance", GUILayout.Width(135f), GUILayout.Height(30f)) &&
                    EmoteController.Play(dances[_selectedDanceIndex], true))
                    Main.CloseRoweModMenu();
                previousEnabled = GUI.enabled;
                GUI.enabled = EmoteController.IsActive || EmoteController.IsLooping;
                if (SecondaryButton("Stop Dance", GUILayout.Width(125f), GUILayout.Height(30f)))
                    EmoteController.Stop();
                GUI.enabled = previousEnabled;
                EndToolbar();
            }
            EndPane();

            DrawEmoteGroup(
                "Added Poses",
                "Sitting and other compatible clips already shipped with the game. These play locally so custom IDs are never sent to players without them.",
                option => !option.Stock && !option.Imported);
            DrawEmoteGroup(
                "Game Emotes",
                "The original emote array. These keep the game's native networking behavior.",
                option => option.Stock);
            DrawEmoteGroup(
                "Imported Animation Clips",
                "Humanoid rider clips found in your RoweMod animation bundles. Trick-specific clips may not look natural as stationary emotes.",
                option => option.Imported);
        }

        private static void DrawEmoteGroup(
            string title,
            string description,
            Func<EmoteController.EmoteOption, bool> include)
        {
            BeginAltPane(title, description);
            IReadOnlyList<EmoteController.EmoteOption> options = EmoteController.Options;
            int shown = 0;
            for (int i = 0; i < options.Count; i++)
            {
                EmoteController.EmoteOption option = options[i];
                if (option == null || !include(option))
                    continue;

                shown++;
                GUILayout.BeginHorizontal(UiRowButtonStyle, GUILayout.MinHeight(42f * UiScale));
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(option.Label, UiRowLabelStyle);
                GUILayout.Label(option.ClipName, UiRowMutedLabelStyle);
                GUILayout.EndVertical();
                if (ControllerButton(
                        $"emote_play_{option.NativeIndex}_{option.ClipName}",
                        "Play",
                        UiButtonStyle,
                        GUILayout.Width(90f * UiScale),
                        GUILayout.Height(30f * UiScale)))
                {
                    if (EmoteController.Play(option))
                        Main.CloseRoweModMenu();
                }
                if (!option.Stock && ControllerButton(
                        $"emote_loop_{option.NativeIndex}_{option.ClipName}",
                        "Loop",
                        UiButtonStyle,
                        GUILayout.Width(90f * UiScale),
                        GUILayout.Height(30f * UiScale)))
                {
                    if (EmoteController.Play(option, true))
                        Main.CloseRoweModMenu();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4f * UiScale);
            }

            if (shown == 0)
                GUILayout.Label("No compatible clips were found in this group.", UiMutedWrappedStyle);
            EndPane();
        }

        private static void DrawMultiplayerPage()
        {
            BeginPane("Player Labels", "Name tag visibility and multiplayer challenge controls.");
            bool previousShowPlayerUserNameTargets = misc.showPlayerUserNameTargets;
            ModernToggle("Show PlayerUserNameTarget", ref misc.showPlayerUserNameTargets,
                "mp_show_player_username_targets");
            if (previousShowPlayerUserNameTargets != misc.showPlayerUserNameTargets)
                ApplyPlayerUserNameTargetsVisibility(true);

            BeginToolbar();
            if (SecondaryButton("Refresh Player Name Targets", GUILayout.Width(205f), GUILayout.Height(26f)))
                ApplyPlayerUserNameTargetsVisibility(true);

            GUI.enabled = MultiplayerChallengeManager.FeatureEnabled;
            if (PrimaryButton(
                    MultiplayerChallengeManager.IsOpen ? "Close Challenge UI" : "Open Challenge UI",
                    GUILayout.Width(160f), GUILayout.Height(26f)))
            {
                MultiplayerChallengeManager.ToggleWindow();
            }
            GUI.enabled = true;
            EndToolbar();

            if (!MultiplayerChallengeManager.FeatureEnabled)
            {
                GUILayout.Label(
                    "MP BIKE challenge work is disabled for now. The code is still kept for later.",
                    UiMutedWrappedStyle);
            }
            EndPane();
        }

        private static void ResetSelectedPage(MenuPage page)
        {
            switch (page)
            {
                case MenuPage.RidePrediction:
                    PredictionSystemsControl.RestoreAllGameValues();
                    predictionLabSettings = new PredictionLabSettings();
                    break;
                case MenuPage.RideSafety:
                    misc.neverBail = false;
                    misc.disableBoneBreaking = false;
                    misc.boneBreakingStrength = 1f;
                    Mods.Misc.ApplyBoneBreakingState(true);
                    break;
                case MenuPage.RideVehicleTuning:
                    physics.bmxForceFactor = 0.07f;
                    physics.bmxMaxSpeed = 7.5f;
                    Config.motorTuning.Clear();
                    RestoreMotorTuningDefaults();
                    _motorTuningNeedsRefresh = true;
                    Mods.Physics.Update();
                    break;
                case MenuPage.RiderEmotes:
                    EmoteController.RestoreOriginalList();
                    break;
                case MenuPage.WorldDrone:
                    misc.droneBodyToggle = true;
                    misc.droneEmitterToggle = true;
                    misc.disableDroneCollider = false;
                    misc.droneMass = 10f;
                    Mods.Misc.Update();
                    break;
                case MenuPage.AdvancedStartup:
                    Config.autoSkipIntro = true;
                    break;
                case MenuPage.AdvancedInterface:
                    misc.menuAccentR = 1f;
                    misc.menuAccentG = 0.54f;
                    misc.menuAccentB = 0.30f;
                    misc.menuScale = 1f;
                    misc.menuDesignVersion = 1;
                    ApplyConfiguredInterfaceScale();
                    break;
                case MenuPage.CameraGameplay:
                    ResetCurrentTab();
                    misc.disableFreeCamCollider = false;
                    break;
                default:
                    ResetCurrentTab();
                    break;
            }

            ResetSliderUI();
            Config.Save();
        }
    }
}
