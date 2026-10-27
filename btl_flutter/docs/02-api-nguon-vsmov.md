# 02 – API nguồn: vsmov.com

> Khảo sát thực tế ngày **27/10/2026**. Không có tài liệu chính thức – mọi thông tin dưới đây rút ra từ phản hồi thật.
> JSON mẫu đầy đủ: [samples/](samples/).

- **Base URL**: `https://vsmov.com/api`
- **Xác thực**: không cần.
- **Định dạng**: JSON, UTF-8 (ký tự tiếng Việt có thể escape `\uXXXX`, dấu `/` escape `\/`).
- **Lỗi**: slug/đường dẫn không tồn tại → `HTTP 404` với **body HTML của nginx** (không phải JSON).
- Nên gửi header `User-Agent` khi gọi từ server.

## 1. Danh sách endpoint

| Mục đích | Endpoint | Ghi chú |
|---|---|---|
| Phim mới cập nhật | `GET /danh-sach/phim-moi-cap-nhat?page={n}` | 24 item/trang |
| Phim bộ | `GET /danh-sach/phim-bo?page={n}` | |
| Phim lẻ | `GET /danh-sach/phim-le?page={n}` | |
| Phim chiếu rạp | `GET /danh-sach/phim-chieu-rap?page={n}` | |
| Tìm kiếm | `GET /tim-kiem?keyword={q}&page={n}` | keyword cần URL-encode |
| Danh sách thể loại | `GET /the-loai` | ~45 thể loại, format khác (xem mục 3) |
| Phim theo thể loại | `GET /the-loai/{slug}?page={n}` | VD: `lang-man`, `chinh-kich` |
| Danh sách quốc gia | `GET /quoc-gia` | format giống `/the-loai` |
| Phim theo quốc gia | `GET /quoc-gia/{slug}?page={n}` | VD: `han-quoc` |
| Phim theo năm | `GET /nam/{year}?page={n}` | VD: `2025` |
| **Chi tiết phim + tập** | `GET /phim/{slug}` | VD: `mua-he-dang-nho-2026` |

Các đường dẫn đã thử và **không tồn tại** (404): `/danh-sach/hoat-hinh`, `/danh-sach/tv-shows`, `/danh-sach/phim-vietsub`.

Tham số `limit` **không đáng tin** (truyền `limit=10` vẫn nhận 20–24 item) → không sử dụng; dùng `pagination.totalItemsPerPage` trả về.

## 2. Danh sách phim (list / search / theo thể loại / quốc gia / năm)

```json
{
  "status": true,
  "items": [
    {
      "tmdb": { "type": "tv", "id": "279923", "season": null, "vote_average": "0.0", "vote_count": 0 },
      "imdb": { "id": "tt35288698" },
      "modified": { "time": "2026-10-27T17:13:06+07:00" },
      "_id": 53718,
      "name": "Túy Mộng",
      "origin_name": "Dream",
      "slug": "tuy-mong",
      "poster_url": "https://vsmov.com/storage/images/lqTioTBVKH3RfdgCe04SR9V8taR.jpg",
      "thumb_url": "https://vsmov.com/storage/images/xvUOJChljBOY9e4vDc36p2yOIOM.jpg",
      "year": 2026
    }
  ],
  "pathImage": "https://nguon.vsphim.com/storage/images/",
  "pagination": { "totalItems": 20810, "totalItemsPerPage": 24, "currentPage": 1, "totalPages": 868 }
}
```

Lưu ý:
- Item danh sách **không có** `quality`, `lang`, `episode_current` → muốn hiển thị phải gọi chi tiết (không nên, tốn request). Card danh sách chỉ hiển thị ảnh, tên, năm, điểm TMDB.
- `tmdb.vote_average` là **chuỗi** (`"7.3"`) → parse sang `double`.
- `poster_url` / `thumb_url` hiện là URL tuyệt đối. Nếu gặp đường dẫn tương đối → ghép với `pathImage`.
- Ảnh `poster_url` rất nặng (đo được ~1.5 MB/ảnh) → app nên giới hạn kích thước decode.
- `pagination` có thể thiếu khi kết quả rỗng → mặc định `totalPages = 0`.

## 3. Thể loại / Quốc gia

```json
{
  "status": "success",
  "message": "",
  "data": {
    "items": [
      { "_id": 85, "name": "Action & Adventure", "slug": "action-adventure" },
      { "_id": 17, "name": "Bí Ẩn", "slug": "bi-an" }
    ]
  }
}
```

Lưu ý: `status` ở đây là **chuỗi** `"success"`, khác với endpoint phim (`true`). Dữ liệu nằm trong `data.items`.

## 4. Chi tiết phim – `GET /phim/{slug}`

Ví dụ: `https://vsmov.com/api/phim/mua-he-dang-nho-2026` (đầy đủ: [samples/phim-detail.json](samples/phim-detail.json))

```json
{
  "status": true,
  "msg": "",
  "movie": {
    "tmdb": { "type": "tv", "id": "335951", "season": null, "vote_average": "0.0", "vote_count": 0 },
    "_id": 54078,
    "name": "Mùa Hè Đáng Nhớ",
    "origin_name": "Summer Vacation",
    "slug": "mua-he-dang-nho-2026",
    "content": "An ordinary teen girl longs for something new – ...",
    "type": "series",
    "status": "ongoing",
    "poster_url": "https://vsmov.com/storage/images/...jpg",
    "thumb_url": "https://vsmov.com/storage/images/...jpg",
    "trailer_url": null,
    "time": "60 phút",
    "episode_current": "Tập 1",
    "episode_total": "10",
    "quality": "HD",
    "lang": "Vietsub",
    "showtimes": "Tập mới được ra mắt vào ngày 02/11/2026",
    "year": 2026,
    "view": 14,
    "chieurap": false,
    "actor": ["Cho Yi-hyun", "Jung Jin-young"],
    "director": ["Song Ji-won"],
    "category": [{ "id": 15, "name": "Chính Kịch", "slug": "chinh-kich" }],
    "country": [{ "id": 10, "name": "Hàn Quốc", "slug": "han-quoc" }]
  },
  "episodes": [
    {
      "server_name": "Vietsub\r\n                        #1",
      "server_data": [
        { "name": "1", "slug": "tap-1", "filename": "1",
          "link_embed": "https://v1.streamvsmov.com/video/364fa3f4-5916-40b8-959f-701a0e9c78cc" }
      ]
    }
  ]
}
```

### Giá trị đã quan sát

| Trường | Giá trị |
|---|---|
| `movie.type` | `series` (phim bộ), `single` (phim lẻ). Có thể còn giá trị khác → xử lý dạng chuỗi tự do |
| `movie.status` | `ongoing`, `completed` (có thể có `trailer`) |
| `episodes[].server_data[].name` | `"1"`, `"2"`… với phim bộ; `"Full"` với phim lẻ |
| `server_name` | Chứa `\r\n` + nhiều khoảng trắng → cần **chuẩn hoá** (gộp whitespace, trim) → `"Vietsub #1"` |
| `trailer_url`, `notify`, `imdb.id` | Có thể `null` |
| `content` | Hiện là text thường; phòng trường hợp có HTML → strip tag |

### Phát video

- Nguồn **chỉ cung cấp `link_embed`** (trang HTML chứa JW Player), **không có** `link_m3u8` → phải phát bằng **WebView**.
- Header trả về: `Content-Security-Policy: frame-ancestors *` → cho phép nhúng từ bất kỳ đâu.
- Domain player thay đổi theo tập (`v1.`, `v8.`, `v14.streamvsmov.com`…) → whitelist theo hậu tố `streamvsmov.com`.
