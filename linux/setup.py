"""Setup configuration for StreamShield Linux."""

from setuptools import setup

setup(
    name="stream-shield-linux",
    version="1.0.0",
    description="Sensitive input field detection for Linux via AT-SPI2",
    author="medy gribkov",
    author_email="mahdy34552@gmail.com",
    url="https://github.com/medy-gribkov/stream-shield",
    license="MIT",
    python_requires=">=3.10",
    py_modules=["main", "detector", "overlay", "tray"],
    install_requires=[
        "PyGObject>=3.42.0",
    ],
    entry_points={
        "console_scripts": [
            "stream-shield=main:main",
        ],
    },
    classifiers=[
        "Development Status :: 4 - Beta",
        "Environment :: X11 Applications :: GTK",
        "Intended Audience :: End Users/Desktop",
        "License :: OSI Approved :: MIT License",
        "Operating System :: POSIX :: Linux",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
        "Topic :: Security",
    ],
)
