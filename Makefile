INPUT  ?=

TARGET      = main
BUILD       = .build

CC          = mcs
RUN         = mono
NUNIT       = nunit-console -nologo

ifdef DEBUG
RUN        += --debug
CFLAGS     += -define:DEBUG -debug
endif

ifdef CSHARP_VERSION
CFLAGS     += -langversion:$(CSHARP_VERSION)
endif

all: run

$(BUILD)/%.exe: src/*.cs
	@echo "*** building $@"
	@mkdir -p $(BUILD)
	@$(CC) $(CFLAGS) -out:$@ $^

run: $(BUILD)/$(TARGET).exe
	@$(RUN) $< $(INPUT)

$(BUILD)/test.dll: test/test_*.cs src/*.cs
	@echo "*** building $@"
	@mkdir -p $(BUILD)
	@$(CC) $(CFLAGS) -t:library -r:nunit.framework.dll -out:$@ $^

test: $(BUILD)/test.dll
	@$(NUNIT) $<

clean:
	@rm -rf $(BUILD) TestResult.xml

-include Makefile.local

.PHONY: test
