
import sys

def count_braces(filename):
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()
    
    open_count = content.count('{')
    close_count = content.count('}')
    
    print(f"File: {filename}")
    print(f"Open braces: {open_count}")
    print(f"Close braces: {close_count}")
    print(f"Difference: {open_count - close_count}")

if __name__ == "__main__":
    count_braces(sys.argv[1])
