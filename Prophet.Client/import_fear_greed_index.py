import time
from datetime import datetime, timezone
import os
import sys
import requests
import pandas as pd

# 兼容从 tools 子目录运行时的 mysql 导入路径
try:
    from mysql import execute_many
except Exception:
    CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
    PROJECT_ROOT = os.path.dirname(CURRENT_DIR)
    if PROJECT_ROOT not in sys.path:
        sys.path.insert(0, PROJECT_ROOT)
    from mysql import execute_many

# API 配置
# Alternative.me 恐惧与贪婪指数 API
# limit=0 表示获取所有历史数据
# format=json 或 format=csv
API_URL_JSON = "https://api.alternative.me/fng/?limit=0&format=json"
API_URL_CSV = "https://api.alternative.me/fng/?limit=0&format=csv"


# 导入恐惧与贪婪指数数据到数据库
def import_fear_greed_index_to_db(limit=0):
    """
    从 Alternative.me API 获取恐惧与贪婪指数数据并导入数据库

    参数:
        limit: 获取的历史数据条数，0 表示获取所有历史数据
    """
    print("=" * 60)
    print("📊 开始导入恐惧与贪婪指数数据到数据库")
    print("=" * 60)

    try:
        # 构建 API URL
        if limit > 0:
            api_url_json = f"https://api.alternative.me/fng/?limit={limit}&format=json"
            api_url_csv = f"https://api.alternative.me/fng/?limit={limit}&format=csv"
        else:
            api_url_json = API_URL_JSON
            api_url_csv = API_URL_CSV

        print(f"🌐 正在从 API 获取数据...")
        print(f"   URL: {api_url_json}")

        # 尝试使用 JSON 格式（更可靠）
        try:
            headers = {"User-Agent": "Mozilla/5.0 (compatible; fear-greed-index-import/1.0)"}
            response = requests.get(api_url_json, headers=headers, timeout=30)
            response.raise_for_status()

            data = response.json()

            if 'data' not in data or not isinstance(data['data'], list):
                print("❌ API 返回数据格式错误")
                return False

            records = data['data']
            print(f"✅ 成功从 API 获取数据")
            print(f"📈 数据条数: {len(records)}")

            # 转换为 DataFrame 进行处理
            df = pd.DataFrame(records)

        except Exception as e:
            print(f"⚠️ JSON 格式获取失败，尝试 CSV 格式: {str(e)}")
            # 回退到 CSV 格式
            response = requests.get(api_url_csv, headers=headers, timeout=30)
            response.raise_for_status()

            # 读取 CSV
            from io import StringIO

            df = pd.read_csv(StringIO(response.text))
            print(f"✅ 成功从 API 获取 CSV 数据")
            print(f"📈 数据条数: {len(df)}")

        # 数据验证
        print("\n🔍 开始数据验证...")
        validation_results = _validate_fear_greed_data(df)

        if not validation_results['is_valid']:
            print("❌ 数据验证失败:")
            for error in validation_results['errors']:
                print(f"   - {error}")
            return False

        print("✅ 数据验证通过")

        # 准备插入数据库的数据
        print("🔄 正在准备数据...")
        sql_data = []

        for _, row in df.iterrows():
            # 处理时间戳（可能是字符串或数字）
            timestamp_raw = row['timestamp']
            timestamp: float

            if isinstance(timestamp_raw, str):
                # 尝试解析字符串时间戳
                try:
                    timestamp = float(timestamp_raw)
                except:
                    # 尝试解析日期字符串
                    try:
                        dt = pd.to_datetime(timestamp_raw)
                        timestamp = float(dt.timestamp())
                    except:
                        print(f"⚠️ 无法解析时间戳: {timestamp_raw}")
                        continue
            else:
                # 确保是数值类型
                try:
                    timestamp = float(timestamp_raw)
                except:
                    print(f"⚠️ 无法转换时间戳为数值: {timestamp_raw}")
                    continue

            # 转换为日期
            date = datetime.fromtimestamp(timestamp, tz=timezone.utc).date()

            # 处理 value
            value = int(row['value'])

            # 处理 classification
            classification = str(row['value_classification']).strip()
            # 限制长度（数据库字段是 varchar(20)）
            if len(classification) > 20:
                classification = classification[:20]

            sql_data.append([date, value, classification])

        print(f"✅ 数据准备完成，共 {len(sql_data)} 条记录")

        # 分批插入数据库
        print("\n💾 开始保存数据到数据库...")
        batch_size = 1000
        total_batches = (len(sql_data) + batch_size - 1) // batch_size
        success_count = 0

        for i in range(0, len(sql_data), batch_size):
            batch_num = (i // batch_size) + 1
            batch_data = sql_data[i : i + batch_size]

            print(f"   处理批次 {batch_num}/{total_batches} ({len(batch_data)} 条记录)...")

            try:
                # 插入数据库（使用 ON DUPLICATE KEY UPDATE 处理重复数据）
                query = """INSERT INTO feargreed(`date`, `value`, `classification`) 
                           VALUES (%s, %s, %s) 
                           ON DUPLICATE KEY UPDATE 
                           `value`=VALUES(`value`), 
                           `classification`=VALUES(`classification`);"""

                execute_many(query, batch_data)

                # 显示进度
                progress = (batch_num / total_batches) * 100
                success_count += len(batch_data)
                print(f"   ✅ 批次 {batch_num} 完成 ({progress:.1f}%)，处理 {len(batch_data)} 条")

            except Exception as e:
                print(f"   ❌ 批次 {batch_num} 失败: {str(e)}")

        # 显示最终结果
        print("=" * 60)
        print("📊 导入完成统计")
        print("=" * 60)
        print(f"✅ 成功导入: {success_count} 条记录")
        print(f"📈 总处理记录: {len(sql_data)} 条")
        print(f"📊 成功率: {(success_count/len(sql_data)*100):.1f}%")

        if success_count > 0:
            print("🎉 恐惧与贪婪指数数据导入成功！")
            return True
        else:
            print("❌ 恐惧与贪婪指数数据导入失败！")
            return False

    except Exception as e:
        print(f"❌ 导入过程中发生错误: {str(e)}")
        import traceback

        traceback.print_exc()
        return False


# 验证恐惧与贪婪指数数据
def _validate_fear_greed_data(df):
    """验证数据格式和内容"""
    errors = []

    if df.empty:
        errors.append("数据为空")
        return {'is_valid': False, 'errors': errors}

    # 检查必需字段
    required_columns = ['timestamp', 'value', 'value_classification']
    for col in required_columns:
        if col not in df.columns:
            errors.append(f"缺少必需列: {col}")

    if errors:
        return {'is_valid': False, 'errors': errors}

    # 检查数据类型
    try:
        # 转换 value 为数值类型
        df['value'] = pd.to_numeric(df['value'], errors='coerce')
    except Exception as e:
        errors.append(f"value 列数据类型转换失败: {str(e)}")

    # 检查数据完整性
    if df['value'].isnull().any():
        errors.append("value 列包含空值")

    if df['timestamp'].isnull().any():
        errors.append("timestamp 列包含空值")

    if df['value_classification'].isnull().any():
        errors.append("value_classification 列包含空值")

    # 检查 value 范围 (0-100)
    invalid_values = df[(df['value'] < 0) | (df['value'] > 100)]
    if not invalid_values.empty:
        errors.append(f"发现 {len(invalid_values)} 条 value 超出范围 (0-100) 的记录")

    return {'is_valid': len(errors) == 0, 'errors': errors}


# 主函数
def main():
    """
    主函数：导入恐惧与贪婪指数数据

    可以通过修改 limit 参数来控制导入的数据量：
    - limit=0: 导入所有历史数据（默认）
    - limit=365: 导入最近一年的数据
    - limit=30: 导入最近30天的数据
    """
    # 导入所有历史数据
    limit = 0  # 0 表示获取所有历史数据

    print(f"📅 导入配置: limit={limit} ({'所有历史数据' if limit == 0 else f'最近{limit}天'})")

    ok = import_fear_greed_index_to_db(limit=limit)

    if not ok:
        print("❌ 导入失败，请检查错误信息")
        return

    print("\n✅ 导入任务完成！")


# 添加主程序入口
if __name__ == "__main__":
    main()
