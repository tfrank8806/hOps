#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace hOps.web.Utilities
{
    public static class LinenInventorySeedData
    {
        private static readonly Lazy<LinenInventoryTemplate> _template = new(() =>
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LinenInventoryTemplate>(DefaultJson, options) ?? new LinenInventoryTemplate();
        });

        public static LinenInventoryTemplate Template => _template.Value;

        private const string DefaultJson = """
{
  "room_types": [
    {
      "name": "Studio Suite King Sofa (STKT)",
      "rooms": 54
    },
    {
      "name": "Corner Studio Suite Queen/Queen (ONQQ)",
      "rooms": 15
    },
    {
      "name": "One Bedroom Suite King (ONBR)",
      "rooms": 16
    },
    {
      "name": "Studio Suite Queen/Queen (STQQ)",
      "rooms": 27
    }
  ],
  "items": [
    {
      "name": "Pillow - Natural",
      "order_item_number": "144174",
      "case_count": 8.0,
      "case_price": 128.67,
      "par_level_target": 2.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 2.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 4.0,
        "One Bedroom Suite King (ONBR)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 4.0
      }
    },
    {
      "name": "Pillow - Synthetic",
      "order_item_number": "398112",
      "case_count": 8.0,
      "case_price": 49.94,
      "par_level_target": 2.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 2.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "One Bedroom Suite King (ONBR)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Pillow Case",
      "order_item_number": "144266",
      "case_count": 72.0,
      "case_price": 144.64,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 4.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 6.0,
        "One Bedroom Suite King (ONBR)": 4.0,
        "Studio Suite Queen/Queen (STQQ)": 6.0
      }
    },
    {
      "name": "Pillow Protector",
      "order_item_number": "144177",
      "case_count": 12.0,
      "case_price": 28.59,
      "par_level_target": 2.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 4.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 6.0,
        "One Bedroom Suite King (ONBR)": 4.0,
        "Studio Suite Queen/Queen (STQQ)": 6.0
      }
    },
    {
      "name": "King Flat Sheet",
      "order_item_number": "144262",
      "case_count": 24.0,
      "case_price": 240.48,
      "par_level_target": 2.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0
      }
    },
    {
      "name": "King Fitted Sheet",
      "order_item_number": "144259",
      "case_count": 24.0,
      "case_price": 314.12,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0
      }
    },
    {
      "name": "King Blanket",
      "order_item_number": "355450",
      "case_count": 4.0,
      "case_price": 176.22,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0
      }
    },
    {
      "name": "King Top Sheet",
      "order_item_number": "144281",
      "case_count": 24.0,
      "case_price": 605.13,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0
      }
    },
    {
      "name": "King Mattress Pad",
      "order_item_number": "267502",
      "case_count": 10.0,
      "case_price": 146.79,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0
      }
    },
    {
      "name": "Queen Flat Sheet",
      "order_item_number": "144260",
      "case_count": 24.0,
      "case_price": 216.23,
      "par_level_target": 3.0,
      "requirements": {
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Queen Fitted Sheet",
      "order_item_number": "144253",
      "case_count": 24.0,
      "case_price": 268.82,
      "par_level_target": 3.0,
      "requirements": {
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Queen Blanket",
      "order_item_number": "355076",
      "case_count": 4.0,
      "case_price": 158.61,
      "par_level_target": 3.0,
      "requirements": {
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Queen Top Sheet",
      "order_item_number": "144282",
      "case_count": 24.0,
      "case_price": 539.86,
      "par_level_target": 3.0,
      "requirements": {
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Queen Mattress Pad",
      "order_item_number": "263324",
      "case_count": 12.0,
      "case_price": 141.18,
      "par_level_target": 3.0,
      "requirements": {
        "Corner Studio Suite Queen/Queen (ONQQ)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 2.0
      }
    },
    {
      "name": "Bath Towels",
      "order_item_number": "330723",
      "case_count": 36.0,
      "case_price": 207.77,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 3.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 4.0,
        "One Bedroom Suite King (ONBR)": 3.0,
        "Studio Suite Queen/Queen (STQQ)": 4.0
      }
    },
    {
      "name": "Hand Towels",
      "order_item_number": "144271",
      "case_count": 120.0,
      "case_price": 154.55,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 2.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 3.0,
        "One Bedroom Suite King (ONBR)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 3.0
      }
    },
    {
      "name": "Washcloth",
      "order_item_number": "142215",
      "case_count": 180.0,
      "case_price": 125.39,
      "par_level_target": 3.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 2.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 3.0,
        "One Bedroom Suite King (ONBR)": 2.0,
        "Studio Suite Queen/Queen (STQQ)": 3.0
      }
    },
    {
      "name": "Bathmat",
      "order_item_number": "144272",
      "case_count": 60.0,
      "case_price": 152.36,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0,
        "Studio Suite Queen/Queen (STQQ)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Fitted Sheet",
      "order_item_number": "762985",
      "case_count": 24.0,
      "case_price": 215.24,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Flat Sheet",
      "order_item_number": "144257",
      "case_count": 24.0,
      "case_price": 199.57,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Blanket",
      "order_item_number": "763086",
      "case_count": 1.0,
      "case_price": 14.33,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Pillow",
      "order_item_number": "398112",
      "case_count": 8.0,
      "case_price": 49.94,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Pillow Protector",
      "order_item_number": "144177",
      "case_count": 12.0,
      "case_price": 28.59,
      "par_level_target": 1.5,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Sofa Bed - Pillow Case",
      "order_item_number": "144266",
      "case_count": 72.0,
      "case_price": 144.64,
      "par_level_target": 1.2,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0
      }
    },
    {
      "name": "Kitchen Towel",
      "order_item_number": "761376",
      "case_count": 12.0,
      "case_price": 17.33,
      "par_level_target": 112.0,
      "requirements": {
        "Studio Suite King Sofa (STKT)": 1.0,
        "Corner Studio Suite Queen/Queen (ONQQ)": 1.0,
        "One Bedroom Suite King (ONBR)": 1.0,
        "Studio Suite Queen/Queen (STQQ)": 1.0
      }
    },
    {
      "name": "Pool Towels",
      "order_item_number": "142494",
      "case_count": 60.0,
      "case_price": 290.87,
      "par_level_target": 112.0,
      "requirements": {}
    },
    {
      "name": "Gym Towels",
      "order_item_number": "147580",
      "case_count": 12.0,
      "case_price": 35.87,
      "par_level_target": 3.0,
      "requirements": {}
    },
    {
      "name": "Crib Sheet",
      "order_item_number": "760807",
      "case_count": 1.0,
      "case_price": 14.76,
      "par_level_target": 3.0,
      "requirements": {}
    },
    {
      "name": "Crib Blanket",
      "order_item_number": "167135",
      "case_count": 6.0,
      "case_price": 98.17,
      "par_level_target": 1.0,
      "requirements": {}
    }
  ]
}
""";
    }

    public class LinenInventoryTemplate
    {
        [JsonPropertyName("room_types")]
        public List<LinenInventoryRoomTypeTemplate> RoomTypes { get; set; } = new();

        [JsonPropertyName("items")]
        public List<LinenInventoryItemTemplate> Items { get; set; } = new();
    }

    public class LinenInventoryRoomTypeTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("rooms")]
        public int Rooms { get; set; }
    }

    public class LinenInventoryItemTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("order_item_number")]
        public string? OrderItemNumber { get; set; }

        [JsonPropertyName("case_count")]
        public decimal CaseCount { get; set; }

        [JsonPropertyName("case_price")]
        public decimal CasePrice { get; set; }

        [JsonPropertyName("par_level_target")]
        public decimal ParLevelTarget { get; set; }

        [JsonPropertyName("requirements")]
        public Dictionary<string, decimal> Requirements { get; set; } = new();
    }
}
