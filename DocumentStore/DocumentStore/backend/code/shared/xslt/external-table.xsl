<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:include href="_table-utils.xsl"/>

    <xsl:param name="table-id">undefined</xsl:param>
    <xsl:param name="workbook-reference">undefined</xsl:param>
    <xsl:param name="taxxor-client-id">undefined</xsl:param>
    <xsl:param name="table-header-wrapper" select="''"/>


    <xsl:output method="xml" indent="yes" omit-xml-declaration="yes"/>


    <xsl:template match="/">
        <xsl:apply-templates select="html/tableDefinition"/>

    </xsl:template>

    <xsl:template match="tableDefinition">
        <xsl:variable name="table-type">
            <xsl:call-template name="detect-table-type"/>
        </xsl:variable>

        <div id="tablewrapper_{$table-id}" data-contenteditable="false" contenteditable="false">
            <xsl:attribute name="class">
                <xsl:text>table-wrapper c-table </xsl:text>
                <xsl:choose>
                    <xsl:when test="$table-type = 'sde'">structured-data-table</xsl:when>
                    <xsl:otherwise>external-table</xsl:otherwise>
                </xsl:choose>
                <xsl:choose>
                    <xsl:when test="$taxxor-client-id = 'philips'">
                        <xsl:text> tablewidth-100</xsl:text>
                    </xsl:when>
                </xsl:choose>
            </xsl:attribute>

            <xsl:choose>
                <xsl:when test="$table-type = 'sde'">
                    <xsl:attribute name="data-instanceid">
                        <xsl:value-of select="/html/head/meta[@name = 'instanceid']/@content"/>
                    </xsl:attribute>
                </xsl:when>
                <xsl:when test="$table-type = 'sdepreview'">
                    <xsl:attribute name="data-sdepreview">true</xsl:attribute>
                </xsl:when>
            </xsl:choose>

            <xsl:apply-templates select="metaData">
                <xsl:with-param name="caption" select="table/caption"/>
            </xsl:apply-templates>

            <xsl:apply-templates select="table"/>

            <div class="tablegraph-footer-wrapper" data-contenteditable="false" contenteditable="false">
                <xsl:text> </xsl:text>
            </div>
        </div>

    </xsl:template>

    <xsl:template match="metaData">
        <xsl:param name="caption"/>
        <xsl:param name="force-header">true</xsl:param>


        <xsl:choose>
            <xsl:when test="$table-header-wrapper">
                <!-- Use the table-header-wrapper parameter -->
                <!--<xsl:copy-of select="$table-header-wrapper"/>-->
                <xsl:apply-templates select="$table-header-wrapper">
                    <xsl:with-param name="caption" select="$caption"/>
                    <xsl:with-param name="force-header" select="$force-header"/>
                    <xsl:with-param name="table-title" select="entry[@key = 'txxCaption']"/>
                    <xsl:with-param name="table-scale" select="entry[@key = 'txxScale']"/>
                    <xsl:with-param name="table-currency" select="entry[@key = 'txxCurrency']"/>
                </xsl:apply-templates>
            </xsl:when>
            <xsl:otherwise>
                <div class="tablegraph-header-wrapper" data-contenteditable="false" contenteditable="false">
                    <xsl:choose>


                        <xsl:when test="$taxxor-client-id = 'philips'">
                            <xsl:if test="$force-header = 'true' or entry[@key = 'entity' and string-length(normalize-space(.)) &gt; 0]">
                                <p class="sectortitle" data-contenteditable="true" contenteditable="true">
                                    <xsl:choose>
                                        <xsl:when test="$force-header = 'true'">sectortitle</xsl:when>
                                        <xsl:otherwise>
                                            <xsl:value-of select="entry[@key = 'entity']"/>
                                        </xsl:otherwise>
                                    </xsl:choose>
                                </p>
                            </xsl:if>

                            <xsl:if test="$force-header = 'true' or entry[(@key = 'title' and string-length(normalize-space(.)) &gt; 0) or (@key = 'unit' and string-length(normalize-space(.)) &gt; 0) or (@key = 'scale' and string-length(normalize-space(.)) &gt; 0) or (string-length(normalize-space($caption)) &gt; 0)]">
                                <xsl:variable name="unit" select="normalize-space(entry[@key = 'unit'])"/>
                                <xsl:variable name="scale" select="normalize-space(entry[@key = 'scale'])"/>

                                <div>
                                    <xsl:if test="$force-header = 'true' or entry[@key = 'title' and string-length(normalize-space(.)) &gt; 0]">
                                        <p class="maintitle" data-contenteditable="true" contenteditable="true">
                                            <xsl:choose>
                                                <xsl:when test="$force-header = 'true'">maintitle</xsl:when>
                                                <xsl:otherwise>
                                                    <xsl:value-of select="entry[@key = 'title']"/>
                                                </xsl:otherwise>
                                            </xsl:choose>
                                        </p>
                                    </xsl:if>
                                    <xsl:if test="$force-header = 'true' or ((string-length(normalize-space($scale)) &gt; 0) and (string-length(normalize-space($unit)) &gt; 0))">
                                        <p class="unittitle" data-contenteditable="true" contenteditable="true">
                                            <xsl:choose>
                                                <xsl:when test="$force-header = 'true'">unittitle</xsl:when>
                                                <xsl:when test="$scale = '1000000'">
                                                    <xsl:text>in millions of </xsl:text>
                                                    <xsl:value-of select="$unit"/>
                                                    <xsl:text> unless otherwise stated</xsl:text>
                                                </xsl:when>
                                                <xsl:otherwise>
                                                    <xsl:value-of select="$scale"/>
                                                    <xsl:text> </xsl:text>
                                                    <xsl:value-of select="$unit"/>
                                                </xsl:otherwise>
                                            </xsl:choose>
                                        </p>
                                    </xsl:if>
                                </div>

                                <xsl:if test="$force-header = 'true' or string-length(normalize-space($caption)) &gt; 0">
                                    <p class="datatitle" data-contenteditable="true" contenteditable="true">
                                        <xsl:choose>
                                            <xsl:when test="$force-header = 'true'">datetitle</xsl:when>
                                            <xsl:otherwise>
                                                <xsl:value-of select="$caption"/>
                                            </xsl:otherwise>
                                        </xsl:choose>
                                    </p>
                                </xsl:if>

                            </xsl:if>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:variable name="table-title" select="entry[@key = 'txxCaption']"/>
                            <xsl:variable name="table-scale" select="entry[@key = 'txxScale']"/>
                            <xsl:variable name="table-currency" select="entry[@key = 'txxCurrency']"/>
                            <div class="tablegraph-header-wrapper" data-contenteditable="false" contenteditable="false">
                                <div class="table-title" data-contenteditable="true" contenteditable="true">
                                    <xsl:choose>
                                        <xsl:when test="string-length(normalize-space($table-title)) &gt; 0">
                                            <xsl:value-of select="$table-title"/>
                                        </xsl:when>
                                        <xsl:otherwise>Title</xsl:otherwise>
                                    </xsl:choose>
                                </div>
                                <div class="table-scale" data-contenteditable="true" contenteditable="true">
                                    <xsl:choose>
                                        <xsl:when test="string-length(normalize-space($table-scale)) &gt; 0 and string-length(normalize-space($table-currency)) &gt; 0">
                                            <xsl:choose>
                                                <xsl:when test="$taxxor-client-id = 'tiekinetix' and $table-scale = '1000'">(&#8364; x 1,000)</xsl:when>
                                                <xsl:when test="$taxxor-client-id = 'tiekinetix' and $table-scale = '10000'">(&#8364; x 10,000)</xsl:when>
                                                <xsl:when test="$taxxor-client-id = 'tiekinetix' and $table-scale = '100000'">(&#8364; x 100,000)</xsl:when>
                                                <xsl:when test="$taxxor-client-id = 'tiekinetix' and $table-scale = '1000000'">(&#8364; x 1,000,000)</xsl:when>
                                                <xsl:otherwise>Scale</xsl:otherwise>
                                            </xsl:choose>
                                        </xsl:when>
                                        <xsl:otherwise>Scale</xsl:otherwise>
                                    </xsl:choose>
                                </div>
                            </div>
                        </xsl:otherwise>

                    </xsl:choose>
                </div>


            </xsl:otherwise>


        </xsl:choose>
    </xsl:template>

    <xsl:template match="div[@class = 'tablegraph-header-wrapper']">
        <xsl:param name="caption"/>
        <xsl:param name="force-header"/>
        <xsl:param name="table-title"/>
        <xsl:param name="table-scale"/>
        <xsl:param name="table-currency"/>




        <!--<xsl:text>-\-\-\-\-\-\-\-\-\-\-\-\-\-</xsl:text>
        <xsl:text>caption: </xsl:text>
        <xsl:value-of select="$caption"/>
        <xsl:text>force-header: </xsl:text>
        <xsl:value-of select="$force-header"/>
        <xsl:text>-\-\-\-\-\-\-\-\-\-\-\-\-\-</xsl:text>-->

        <xsl:copy-of select="."/>
    </xsl:template>

    <xsl:template match="table">

        <xsl:variable name="table-type">
            <xsl:call-template name="detect-table-type"/>
        </xsl:variable>

        <xsl:variable name="count-start">
            <xsl:choose>
                <xsl:when test="thead">
                    <xsl:variable name="nr-head-rows">
                        <xsl:value-of select="count(thead/tr)"/>
                    </xsl:variable>
                    <xsl:value-of select="$nr-head-rows + 1"/>
                </xsl:when>
                <xsl:otherwise>1</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>

        <table id="table_{$table-id}" data-workbookreference="{$workbook-reference}" class="tabletype-numbers" data-contenteditable="true" contenteditable="true">
            <xsl:if test="$table-type = 'sde'">
                <xsl:attribute name="data-instanceid">
                    <xsl:value-of select="/html/head/meta[@name = 'instanceid']/@content"/>
                </xsl:attribute>
            </xsl:if>

            <xsl:apply-templates select="thead"/>

            <xsl:apply-templates select="tbody">
                <xsl:with-param name="count-start" select="$count-start"/>
            </xsl:apply-templates>

        </table>



    </xsl:template>

    <xsl:template match="thead">
        <thead>
            <xsl:apply-templates/>
        </thead>
    </xsl:template>

    <xsl:template match="tbody">
        <xsl:param name="count-start"/>
        <tbody>
            <xsl:apply-templates>
                <xsl:with-param name="count-start" select="$count-start"/>
            </xsl:apply-templates>
        </tbody>

    </xsl:template>

    <xsl:template match="thead/tr">
        <xsl:variable name="row-pos" select="count(current()/preceding-sibling::tr) + 1"/>


        <xsl:variable name="first-row">
            <xsl:call-template name="is-first-row">
                <xsl:with-param name="row" select="current()"/>
            </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="last-row">
            <xsl:call-template name="is-last-row">
                <xsl:with-param name="row" select="current()"/>
            </xsl:call-template>
        </xsl:variable>

        <tr>
            <xsl:attribute name="class">
                <xsl:text>r-</xsl:text>
                <xsl:value-of select="$row-pos"/>
                <xsl:if test="$first-row = 'true'">
                    <xsl:text> first</xsl:text>
                </xsl:if>
                <xsl:if test="$last-row = 'true'">
                    <xsl:text> last</xsl:text>
                </xsl:if>
                <xsl:text> </xsl:text>
                <xsl:choose>
                    <xsl:when test="@totalLine = 'total'">total</xsl:when>
                    <xsl:when test="@totalLine = 'grandtotal'">grandtotal</xsl:when>
                    <xsl:otherwise>default</xsl:otherwise>
                </xsl:choose>
            </xsl:attribute>


            <xsl:apply-templates select="th">
                <xsl:with-param name="row-position" select="$row-pos"/>
            </xsl:apply-templates>
        </tr>
    </xsl:template>


    <xsl:template match="tbody/tr">
        <xsl:param name="count-start">1</xsl:param>
        <xsl:variable name="row-pos" select="count(current()/preceding-sibling::tr) + number($count-start)"/>


        <xsl:variable name="first-row">
            <xsl:call-template name="is-first-row">
                <xsl:with-param name="row" select="current()"/>
            </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="last-row">
            <xsl:call-template name="is-last-row">
                <xsl:with-param name="row" select="current()"/>
            </xsl:call-template>
        </xsl:variable>

        <!-- Detect if this row needs to be hidden in the UI -->
        <xsl:variable name="hidden-row">
            <xsl:choose>
                <xsl:when test="count(td) = count(td[contains(@class, 'hidden')])">yes</xsl:when>
                <xsl:otherwise>no</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>

        <tr>
            <xsl:attribute name="class">
                <xsl:text>r-</xsl:text>
                <xsl:value-of select="$row-pos"/>
                <xsl:if test="$first-row = 'true' and number($count-start) = 1">
                    <xsl:text> first</xsl:text>
                </xsl:if>
                <xsl:if test="$last-row = 'true'">
                    <xsl:text> last</xsl:text>
                </xsl:if>
                <xsl:text> </xsl:text>
                <xsl:choose>
                    <xsl:when test="@totalLine = 'total'">total</xsl:when>
                    <xsl:when test="@totalLine = 'grandtotal'">grandtotal</xsl:when>
                    <xsl:otherwise>default</xsl:otherwise>
                </xsl:choose>
                <xsl:if test="$hidden-row = 'yes'"> hide</xsl:if>
            </xsl:attribute>

            <xsl:if test="$hidden-row = 'yes'">
                <xsl:attribute name="data-hiddenrow">true</xsl:attribute>
            </xsl:if>


            <xsl:apply-templates select="td">
                <xsl:with-param name="row-position" select="$row-pos"/>
            </xsl:apply-templates>
        </tr>
    </xsl:template>


    <xsl:template match="td | th">
        <xsl:param name="row-position">0</xsl:param>

        <xsl:variable name="has-no-content">
            <xsl:call-template name="has-no-content">
                <xsl:with-param name="element" select="value[1]"/>
            </xsl:call-template>
        </xsl:variable>

        <xsl:variable name="text">
            <xsl:call-template name="get-paragraph-text">
                <xsl:with-param name="par" select="value[1]"/>
            </xsl:call-template>
        </xsl:variable>

        <xsl:variable name="hidden-cell">
            <xsl:choose>
                <xsl:when test="contains(@class, 'hidden')">yes</xsl:when>
                <xsl:otherwise>no</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>

        <xsl:variable name="cell-position">
            <xsl:call-template name="get-cell-position">
                <xsl:with-param name="cell" select="."/>
            </xsl:call-template>
        </xsl:variable>

        <xsl:variable name="element-name" select="local-name()"/>

        <xsl:element name="{$element-name}">
            <!-- handle column and row spans -->
            <xsl:if test="@colspan and number(@colspan) &gt; 1">
                <xsl:attribute name="colspan">
                    <xsl:value-of select="@colspan"/>
                </xsl:attribute>
            </xsl:if>
            <xsl:if test="@rowspan and number(@rowspan) &gt; 1">
                <xsl:attribute name="rowspan">
                    <xsl:value-of select="@rowspan"/>
                </xsl:attribute>
            </xsl:if>

            <xsl:attribute name="class">
                <!-- cell position -->
                <xsl:text>c-</xsl:text>
                <xsl:value-of select="$cell-position"/>


                <!-- class based on cell content -->

                <xsl:text> datatype-</xsl:text>

                <xsl:choose>
                    <xsl:when test="ul | ol">list</xsl:when>
                    <xsl:when test="string-length($text) = 0">empty</xsl:when>
                    <xsl:when test="$text = '-' or $text = '−' or $text = 'x' or $text = 'X' or $text = '&#xA0;'">empty</xsl:when>
                    <xsl:when test="@format = 'rate-indicator'">rate</xsl:when>
                    <xsl:when test="count(*[1]/intraDocumentSectionLink) = count(*[1]/*) and not(*[1]/text()[normalize-space(.) != ''])">link</xsl:when>
                    <xsl:otherwise>
                        <xsl:variable name="is-negative-value">
                            <xsl:call-template name="is-negative-value">
                                <xsl:with-param name="value" select="$text"/>
                            </xsl:call-template>
                        </xsl:variable>

                        <xsl:choose>
                            <xsl:when test="$is-negative-value = 'true'">negativenumber</xsl:when>
                            <xsl:otherwise>
                                <xsl:variable name="is-first-column">
                                    <xsl:call-template name="is-first-column">
                                        <xsl:with-param name="cell" select="."/>
                                    </xsl:call-template>
                                </xsl:variable>
                                <xsl:variable name="is-year-value">
                                    <xsl:call-template name="is-year-value">
                                        <xsl:with-param name="value" select="$text"/>
                                    </xsl:call-template>
                                </xsl:variable>

                                <xsl:choose>
                                    <xsl:when test="(count(ancestor::tr/preceding-sibling::tr) &lt; 2 or $is-first-column = 'true') and $is-year-value = 'true'">year</xsl:when>

                                    <xsl:otherwise>
                                        <xsl:variable name="is-numeric-value">
                                            <xsl:call-template name="is-numeric-value">
                                                <xsl:with-param name="value" select="$text"/>
                                            </xsl:call-template>
                                        </xsl:variable>
                                        <xsl:choose>
                                            <xsl:when test="$is-numeric-value = 'true'">positivenumber</xsl:when>
                                            <xsl:otherwise>text</xsl:otherwise>
                                        </xsl:choose>
                                    </xsl:otherwise>
                                </xsl:choose>
                            </xsl:otherwise>

                        </xsl:choose>
                    </xsl:otherwise>

                </xsl:choose>

                <xsl:if test="$hidden-cell = 'yes'"> hide</xsl:if>
            </xsl:attribute>

            <xsl:if test="$hidden-cell = 'yes'">
                <xsl:attribute name="data-hiddencell">true</xsl:attribute>
            </xsl:if>

            <xsl:if test="@data-celltype = 'header' or ($element-name = 'th' and preceding-sibling::th[@data-celltype = 'header'])">
                <!--<xsl:attribute name="data-celltype">header</xsl:attribute>-->
                <!-- Calculate an ID based on combination of position and content -->
                <xsl:variable name="cell-position-clean">
                    <xsl:choose>
                        <xsl:when test="contains($cell-position, ' first')">
                            <xsl:call-template name="string-replace-all">
                                <xsl:with-param name="text" select="$cell-position"/>
                                <xsl:with-param name="replace"> first</xsl:with-param>
                                <xsl:with-param name="by"/>
                            </xsl:call-template>
                        </xsl:when>
                        <xsl:when test="contains($cell-position, ' last')">
                            <xsl:call-template name="string-replace-all">
                                <xsl:with-param name="text" select="$cell-position"/>
                                <xsl:with-param name="replace"> last</xsl:with-param>
                                <xsl:with-param name="by"/>
                            </xsl:call-template>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="$cell-position"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:variable>
                <xsl:attribute name="data-cellidentifier">
                    <xsl:value-of select="concat('r', $row-position, '--c', $cell-position-clean, ':', normalize-space(value))"/>
                </xsl:attribute>
            </xsl:if>

            <xsl:choose>
                <xsl:when test="@factId">
                    <span data-fact-id="{@factId}">
                        <xsl:variable name="sde-value" select="value"/>
                        <xsl:choose>
                            <xsl:when test="string-length(normalize-space($sde-value)) &gt; 0">
                                <xsl:value-of select="$sde-value"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:comment>.</xsl:comment>
                            </xsl:otherwise>
                        </xsl:choose>
                    </span>
                </xsl:when>
                <xsl:when test="@data-id">
                    <span data-fact-id="{@data-id}" data-nosdsmapping="true">
                        <xsl:call-template name="format-number">
                            <xsl:with-param name="value" select="value"/>
                        </xsl:call-template>
                    </span>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:call-template name="format-number">
                        <xsl:with-param name="value" select="value"/>
                    </xsl:call-template>
                </xsl:otherwise>
            </xsl:choose>

        </xsl:element>


    </xsl:template>

    <xsl:template name="detect-table-type">
        <xsl:choose>
            <xsl:when test="/html/head/meta[@name = 'instanceid']">sde</xsl:when>
            <xsl:when test="/html/tableDefinition/table/tbody/tr/td[@data-period] and /html/tableDefinition/table/tbody/tr/td[@data-end]">sdepreview</xsl:when>
            <xsl:otherwise>standard</xsl:otherwise>
        </xsl:choose>
    </xsl:template>


</xsl:stylesheet>
