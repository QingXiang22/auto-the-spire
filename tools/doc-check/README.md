# 文档检查

在仓库根目录使用 Python 3.12、Git 和 Vale 3.24.0。无需 .NET、Node.js、拼写字典或额外的 Vale 风格包。

## 安装 Vale

Windows x64 在 PowerShell 中执行，选择自己的工具目录：

```powershell
./tools/doc-check/install_vale.ps1 -InstallDir F:/tool/vale
```

安装脚本从[官方发布](https://github.com/vale-cli/vale/releases/tag/v3.24.0)下载固定版本并校验 SHA256。将安装目录加入 PATH，或通过检查命令的 `--vale` 参数指定 `vale.exe`。已有安装须先核对版本，安装脚本不会直接覆盖已有程序。

## 执行检查

```powershell
python tools/doc-check/check_docs.py
python tools/doc-check/check_terms.py
git diff --check
```

也可以运行统一入口 `python tools/ci/check.py docs`。未安装 Vale、版本不符、规则配置错误或受限表达命中时，检查返回非零；CI 会保留命令输出。

`check_terms.py` 只负责核对版本、获取 Git 文件列表和分批调用 Vale。规则位于[项目风格](../../.github/styles/Project/Terms.yml)，含义与推荐写法见[术语规范](../../docs/meta/glossary.md)。

## 检查范围

- Vale 检查全部 Git 纳管及待提交、未忽略的 Markdown，包括根和模块 `AGENTS.md`、迁移参考目录的 README；不依赖某份旧报告或只扫描 PR 新增行。
- [Vale Markdown 解析](https://docs.vale.sh/formats/markdown)保留代码块、行内代码、公式和 URL 的准确引用。源码、协议样本和固定输入数据的字面内容按原样使用。
- 术语解释页需要展示受限说法，因此仅在该页关闭项目术语规则；其链接和格式仍由文档检查验证。其他说明文档没有整页例外。
- 项目规则检查受限协作表达以及明确用于工程进度的结果统计用语；游戏中的伤害、战斗和效果处理用语仍按原有含义使用。Vale 提供措辞提示，含义和行为要求由评审确认。
