# MacCompare Build & Packaging Scripts

本目录包含 MacCompare 的构建、测试与通用二进制打包分发脚本。  
This directory contains the build, test, and packaging scripts for MacCompare.

👉 **完整中英双语打包指南请查阅 / Please refer to the complete bilingual packaging guide:**  
**[PACKAGING.md](PACKAGING.md)**

---

### 📂 脚本清单 / Script Index

| 脚本文件 / Script | 说明 (中文) | Description (English) |
| :--- | :--- | :--- |
| **`package_dmg.sh`** | 一键编译 Universal Binary 2 并生成 `.dmg` 安装包 | Build Universal Binary 2 and package into `.dmg` installer |
| **`build_universal_lib.sh`** | 交叉编译 Rust 静态库核心 (`libmaccompare_ffi.a`) | Cross-compile Rust core library for `arm64` + `x86_64` via `lipo` |
| **`run_app.sh`** | 本地编译并运行全套测试验证 | Compile locally and run the complete test suite |
| **`generate_bindings.sh`** | 自动生成 Rust FFI C-ABI 桥接头文件 | Generate Rust C-ABI bridge header files |
