namespace ServiceLib.Common;

public static class CloudflareColoMapper
{
    public static readonly string[] TraceUrls =
    [
        "https://speed.cloudflare.com/cdn-cgi/trace",
        "https://www.cloudflare.com/cdn-cgi/trace",
        "https://one.one.one.one/cdn-cgi/trace",
    ];

    public const string Unknown = "-";

    private static readonly Regex ColoRegex = new(@"(?im)^colo=(\w+)\s*$", RegexOptions.Compiled);
    private static readonly Regex LocRegex = new(@"(?im)^loc=([A-Z]{2})\s*$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ColoRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        { "SJC", "美国·圣何塞" },
        { "SFO", "美国·旧金山" },
        { "LAX", "美国·洛杉矶" },
        { "SEA", "美国·西雅图" },
        { "PDX", "美国·波特兰" },
        { "LAS", "美国·拉斯维加斯" },
        { "PHX", "美国·凤凰城" },
        { "DEN", "美国·丹佛" },
        { "DFW", "美国·达拉斯" },
        { "IAH", "美国·休斯敦" },
        { "ORD", "美国·芝加哥" },
        { "MCI", "美国·堪萨斯城" },
        { "MSP", "美国·明尼阿波利斯" },
        { "DTW", "美国·底特律" },
        { "STL", "美国·圣路易斯" },
        { "BNA", "美国·纳什维尔" },
        { "ATL", "美国·亚特兰大" },
        { "MIA", "美国·迈阿密" },
        { "TPA", "美国·坦帕" },
        { "CLT", "美国·夏洛特" },
        { "IAD", "美国·阿什本" },
        { "DCA", "美国·华盛顿" },
        { "EWR", "美国·纽瓦克" },
        { "JFK", "美国·纽约" },
        { "BOS", "美国·波士顿" },
        { "PIT", "美国·匹兹堡" },
        { "CMH", "美国·哥伦布" },
        { "SLC", "美国·盐湖城" },
        { "SAN", "美国·圣迭戈" },
        { "SMF", "美国·萨克拉门托" },
        { "HNL", "美国·檀香山" },
        { "YVR", "加拿大·温哥华" },
        { "YYC", "加拿大·卡尔加里" },
        { "YWG", "加拿大·温尼伯" },
        { "YYZ", "加拿大·多伦多" },
        { "YUL", "加拿大·蒙特利尔" },
        { "MEX", "墨西哥·墨西哥城" },
        { "QRO", "墨西哥·克雷塔罗" },
        { "GDL", "墨西哥·瓜达拉哈拉" },
        { "MTY", "墨西哥·蒙特雷" },

        // Europe
        { "AMS", "荷兰·阿姆斯特丹" },
        { "ARN", "瑞典·斯德哥尔摩" },
        { "ATH", "希腊·雅典" },
        { "BCN", "西班牙·巴塞罗那" },
        { "BEG", "塞尔维亚·贝尔格莱德" },
        { "BER", "德国·柏林" },
        { "BRU", "比利时·布鲁塞尔" },
        { "BUD", "匈牙利·布达佩斯" },
        { "CDG", "法国·巴黎" },
        { "CPH", "丹麦·哥本哈根" },
        { "DUB", "爱尔兰·都柏林" },
        { "DUS", "德国·杜塞尔多夫" },
        { "FCO", "意大利·罗马" },
        { "FRA", "德国·法兰克福" },
        { "GVA", "瑞士·日内瓦" },
        { "HAM", "德国·汉堡" },
        { "HEL", "芬兰·赫尔辛基" },
        { "IST", "土耳其·伊斯坦布尔" },
        { "KEF", "冰岛·雷克雅未克" },
        { "KBP", "乌克兰·基辅" },
        { "KIV", "摩尔多瓦·基希讷乌" },
        { "LGW", "英国·伦敦" },
        { "LHR", "英国·伦敦" },
        { "LIS", "葡萄牙·里斯本" },
        { "LUX", "卢森堡" },
        { "MAD", "西班牙·马德里" },
        { "MAN", "英国·曼彻斯特" },
        { "MRS", "法国·马赛" },
        { "MXP", "意大利·米兰" },
        { "OSL", "挪威·奥斯陆" },
        { "OTP", "罗马尼亚·布加勒斯特" },
        { "PRG", "捷克·布拉格" },
        { "RIX", "拉脱维亚·里加" },
        { "SOF", "保加利亚·索非亚" },
        { "STR", "德国·斯图加特" },
        { "TLL", "爱沙尼亚·塔林" },
        { "VIE", "奥地利·维也纳" },
        { "VNO", "立陶宛·维尔纽斯" },
        { "WAW", "波兰·华沙" },
        { "ZAG", "克罗地亚·萨格勒布" },
        { "ZRH", "瑞士·苏黎世" },

        // Asia
        { "HKG", "香港" },
        { "NRT", "日本·东京" },
        { "HND", "日本·东京" },
        { "KIX", "日本·大阪" },
        { "FUK", "日本·福冈" },
        { "NGO", "日本·名古屋" },
        { "ICN", "韩国·首尔" },
        { "TPE", "台湾·台北" },
        { "KHH", "台湾·高雄" },
        { "SIN", "新加坡" },
        { "KUL", "马来西亚·吉隆坡" },
        { "BKK", "泰国·曼谷" },
        { "DMK", "泰国·曼谷" },
        { "CNX", "泰国·清迈" },
        { "HAN", "越南·河内" },
        { "SGN", "越南·胡志明市" },
        { "MNL", "菲律宾·马尼拉" },
        { "CEB", "菲律宾·宿务" },
        { "CGK", "印度尼西亚·雅加达" },
        { "DPS", "印度尼西亚·巴厘岛" },
        { "SUB", "印度尼西亚·泗水" },
        { "BOM", "印度·孟买" },
        { "DEL", "印度·德里" },
        { "MAA", "印度·金奈" },
        { "BLR", "印度·班加罗尔" },
        { "HYD", "印度·海得拉巴" },
        { "CCU", "印度·加尔各答" },
        { "AMD", "印度·艾哈迈达巴德" },
        { "COK", "印度·科钦" },
        { "DAC", "孟加拉国·达卡" },
        { "KTM", "尼泊尔·加德满都" },
        { "CMB", "斯里兰卡·科伦坡" },
        { "KHI", "巴基斯坦·卡拉奇" },
        { "LHE", "巴基斯坦·拉合尔" },
        { "ISB", "巴基斯坦·伊斯兰堡" },
        { "MLE", "马尔代夫·马累" },
        { "DXB", "阿联酋·迪拜" },
        { "AUH", "阿联酋·阿布扎比" },
        { "DOH", "卡塔尔·多哈" },
        { "MCT", "阿曼·马斯喀特" },
        { "KWI", "科威特" },
        { "RUH", "沙特阿拉伯·利雅得" },
        { "JED", "沙特阿拉伯·吉达" },
        { "BAH", "巴林" },
        { "TLV", "以色列·特拉维夫" },
        { "AMM", "约旦·安曼" },
        { "BEY", "黎巴嫩·贝鲁特" },
        { "EVN", "亚美尼亚·埃里温" },
        { "TBS", "格鲁吉亚·第比利斯" },
        { "ALA", "哈萨克斯坦·阿拉木图" },
        { "NQZ", "哈萨克斯坦·阿斯塔纳" },
        { "TAS", "乌兹别克斯坦·塔什干" },
        { "FRU", "吉尔吉斯斯坦·比什凯克" },
        { "ULN", "蒙古·乌兰巴托" },
        { "PEK", "中国·北京" },
        { "PVG", "中国·上海" },
        { "CAN", "中国·广州" },
        { "SZX", "中国·深圳" },

        // Oceania
        { "SYD", "澳大利亚·悉尼" },
        { "MEL", "澳大利亚·墨尔本" },
        { "BNE", "澳大利亚·布里斯班" },
        { "PER", "澳大利亚·珀斯" },
        { "ADL", "澳大利亚·阿德莱德" },
        { "AKL", "新西兰·奥克兰" },
        { "CHC", "新西兰·基督城" },

        // South America
        { "GRU", "巴西·圣保罗" },
        { "VCP", "巴西·坎皮纳斯" },
        { "GIG", "巴西·里约热内卢" },
        { "CNF", "巴西·贝洛奥里藏特" },
        { "FOR", "巴西·福塔莱萨" },
        { "POA", "巴西·阿雷格里港" },
        { "REC", "巴西·累西腓" },
        { "SSA", "巴西·萨尔瓦多" },
        { "EZE", "阿根廷·布宜诺斯艾利斯" },
        { "SCL", "智利·圣地亚哥" },
        { "LIM", "秘鲁·利马" },
        { "BOG", "哥伦比亚·波哥大" },
        { "MDE", "哥伦比亚·麦德林" },
        { "UIO", "厄瓜多尔·基多" },
        { "GYE", "厄瓜多尔·瓜亚基尔" },
        { "MVD", "乌拉圭·蒙得维的亚" },
        { "ASU", "巴拉圭·亚松森" },

        // Africa
        { "JNB", "南非·约翰内斯堡" },
        { "CPT", "南非·开普敦" },
        { "DUR", "南非·德班" },
        { "NBO", "肯尼亚·内罗毕" },
        { "LOS", "尼日利亚·拉各斯" },
        { "ABV", "尼日利亚·阿布贾" },
        { "ACC", "加纳·阿克拉" },
        { "CAI", "埃及·开罗" },
        { "ALG", "阿尔及利亚·阿尔及尔" },
        { "CMN", "摩洛哥·卡萨布兰卡" },
        { "TUN", "突尼斯" },
        { "ADD", "埃塞俄比亚·亚的斯亚贝巴" },
        { "DAR", "坦桑尼亚·达累斯萨拉姆" },
        { "MPM", "莫桑比克·马普托" },
        { "LAD", "安哥拉·罗安达" },
        { "DKR", "塞内加尔·达喀尔" },
    };

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AE", "阿联酋" },
        { "AR", "阿根廷" },
        { "AT", "奥地利" },
        { "AU", "澳大利亚" },
        { "BE", "比利时" },
        { "BG", "保加利亚" },
        { "BH", "巴林" },
        { "BR", "巴西" },
        { "CA", "加拿大" },
        { "CH", "瑞士" },
        { "CL", "智利" },
        { "CN", "中国" },
        { "CO", "哥伦比亚" },
        { "CZ", "捷克" },
        { "DE", "德国" },
        { "DK", "丹麦" },
        { "EG", "埃及" },
        { "ES", "西班牙" },
        { "FI", "芬兰" },
        { "FR", "法国" },
        { "GB", "英国" },
        { "GR", "希腊" },
        { "HK", "香港" },
        { "HU", "匈牙利" },
        { "ID", "印度尼西亚" },
        { "IE", "爱尔兰" },
        { "IL", "以色列" },
        { "IN", "印度" },
        { "IT", "意大利" },
        { "JP", "日本" },
        { "KR", "韩国" },
        { "KZ", "哈萨克斯坦" },
        { "LU", "卢森堡" },
        { "MX", "墨西哥" },
        { "MY", "马来西亚" },
        { "NL", "荷兰" },
        { "NO", "挪威" },
        { "NZ", "新西兰" },
        { "PH", "菲律宾" },
        { "PL", "波兰" },
        { "PT", "葡萄牙" },
        { "RO", "罗马尼亚" },
        { "RS", "塞尔维亚" },
        { "RU", "俄罗斯" },
        { "SA", "沙特阿拉伯" },
        { "SE", "瑞典" },
        { "SG", "新加坡" },
        { "TH", "泰国" },
        { "TR", "土耳其" },
        { "TW", "台湾" },
        { "UA", "乌克兰" },
        { "US", "美国" },
        { "VN", "越南" },
        { "ZA", "南非" },
    };

    public static string FromTrace(string? traceText)
    {
        if (traceText.IsNullOrEmpty())
        {
            return Unknown;
        }

        var colo = MatchValue(ColoRegex, traceText);
        var loc = MatchValue(LocRegex, traceText);

        return ToRegion(colo, loc);
    }

    public static string ToRegion(string? colo, string? loc = null)
    {
        colo = colo?.Trim().ToUpperInvariant();
        loc = loc?.Trim().ToUpperInvariant();

        if (colo.IsNotEmpty() && ColoRegions.TryGetValue(colo, out var region))
        {
            return region;
        }

        if (loc.IsNotEmpty() && CountryNames.TryGetValue(loc, out var country))
        {
            return colo.IsNotEmpty() ? $"{country}·{colo}" : country;
        }

        return colo.IsNotEmpty() ? colo! : Unknown;
    }

    private static string? MatchValue(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }
}
