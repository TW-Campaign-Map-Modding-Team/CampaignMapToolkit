using System;
using System.Collections.Generic;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Centralised mapping between CAIME's <see cref="GameTemplate"/> and the game key RPFM expects.
    /// Keeps the RPFM-specific knowledge in a single place so the rest of the codebase never switches
    /// on game type for RPFM.
    ///
    /// Only games RPFM supports for this workflow are listed here; anything absent is treated as
    /// "not supported" (see <see cref="IsSupported"/>).
    /// </summary>
    public static class GameMappingProvider
    {
        // GameTemplate -> the game key rpfm_server's SetGameSelected takes. Games not present are
        // unsupported by the RPFM workflow.
        private static readonly Dictionary<GameTemplate, string> GameKeys = new Dictionary<GameTemplate, string>
        {
            [GameTemplate.Rome2]                = "rome_2",
            [GameTemplate.Attila]               = "attila",
            [GameTemplate.Thrones_Of_Britannia] = "thrones_of_britannia",
            [GameTemplate.Warhammer]            = "warhammer",
            [GameTemplate.Warhammer2]           = "warhammer_2",
            [GameTemplate.Warhammer3]           = "warhammer_3",
            [GameTemplate.Three_Kingdoms]       = "three_kingdoms",
            [GameTemplate.Troy]                 = "troy",
            [GameTemplate.Pharaoh]              = "pharaoh",
            [GameTemplate.Pharaoh_Dynasties]    = "pharaoh_dynasties",
        };

        /// <summary>True when the RPFM workflow supports the given game.</summary>
        public static bool IsSupported(GameTemplate game) => GameKeys.ContainsKey(game);

        /// <summary>
        /// The key RPFM identifies the game by, e.g. <c>warhammer_3</c>.
        /// Throws <see cref="NotSupportedException"/> for unsupported games.
        /// </summary>
        public static string GetGameKey(GameTemplate game)
        {
            if (GameKeys.TryGetValue(game, out var gameKey))
            {
                return gameKey;
            }

            throw new NotSupportedException($"The RPFM workflow does not support {game}.");
        }
    }
}
